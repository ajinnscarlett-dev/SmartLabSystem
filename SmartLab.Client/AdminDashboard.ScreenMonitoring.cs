using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client;

public partial class AdminDashboard
{
    private DispatcherTimer? _screenThumbnailTimer;
    private readonly CancellationTokenSource _thumbnailLifetime = new();
    private readonly HashSet<int> _screenInFlight = new();
    private readonly Dictionary<int, DateTime> _screenAttempts = new();
    private readonly Dictionary<int, DateTime> _screenStarted = new();
    private readonly Dictionary<int, DateTime> _screenUpdated = new();
    private readonly Dictionary<int, int?> _screenOwners = new();
    private readonly HashSet<int> _screenMonitoringStarted = new();
    private readonly Dictionary<int, long> _screenVersions = new();
    private readonly Dictionary<int, byte[]> _latestScreenImages = new();
    private readonly Dictionary<int, ImageSource> _decodedScreens = new();
    private readonly Dictionary<int, Image> _screenImageControls = new();

    private void InitializeScreenMonitoring()
    {
        if (_screenThumbnailTimer != null || _thumbnailLifetime.IsCancellationRequested) return;
        _screenThumbnailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _screenThumbnailTimer.Tick += ScreenThumbnailTimer_Tick;
        _screenThumbnailTimer.Start();
    }

    private void StopScreenMonitoring()
    {
        _screenThumbnailTimer?.Stop();
        if (_screenThumbnailTimer != null) _screenThumbnailTimer.Tick -= ScreenThumbnailTimer_Tick;
        _thumbnailLifetime.Cancel();
        _screenImageControls.Clear();
        _latestScreenImages.Clear();
        _decodedScreens.Clear();
    }

    private void ScreenThumbnailTimer_Tick(object? sender, EventArgs e) => _ = RefreshScreenMetadataAsync();

    private Task RefreshScreenMetadataAsync()
    {
        if (!IsLoaded || _thumbnailLifetime.IsCancellationRequested) return Task.CompletedTask;
        var cards = PcGrid.Children.OfType<Border>().Where(c => c.Tag is PCInfo).ToList();
        DateTime now = DateTime.UtcNow;
        foreach (var card in cards)
        {
            var pc = (PCInfo)card.Tag;
            if (!IsPcOccupied(pc) || (_screenOwners.TryGetValue(pc.PcId, out int? owner) && owner != pc.CurrentUserId))
            {
                _latestScreenImages.Remove(pc.PcId);
                _decodedScreens.Remove(pc.PcId);
                _screenVersions.Remove(pc.PcId);
                _screenUpdated.Remove(pc.PcId);
                _screenStarted.Remove(pc.PcId);
                _screenImageControls.Remove(pc.PcId);
                if (FindMonitorFrame(card) is Border frame && frame.Child is Image) frame.Child = null;
            }
            _screenOwners[pc.PcId] = pc.CurrentUserId;
            SetPreviewAge(card, pc.PcId);
        }
        var occupied = cards.Where(c => IsPcOccupied((PCInfo)c.Tag)).ToDictionary(c => ((PCInfo)c.Tag).PcId);
        var candidates = occupied.Where(p => !_screenInFlight.Contains(p.Key)).Select(p =>
            new ThumbnailRefreshPolicy.Candidate(p.Key, IsCardVisible(p.Value), _screenAttempts.GetValueOrDefault(p.Key)));
        foreach (int id in ThumbnailRefreshPolicy.Select(candidates, now, Math.Max(0, 4 - _screenInFlight.Count)))
        {
            _screenAttempts[id] = now;
            _screenInFlight.Add(id);
            _ = RefreshScheduledCardAsync(occupied[id], (PCInfo)occupied[id].Tag);
        }
        return Task.CompletedTask;
    }

    private bool IsCardVisible(Border card)
    {
        if (!card.IsVisible) return false;
        DependencyObject? parent = VisualTreeHelper.GetParent(card);
        while (parent != null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not ScrollViewer viewport) return true;
        return card.TransformToAncestor(viewport).TransformBounds(new Rect(card.RenderSize))
            .IntersectsWith(new Rect(viewport.RenderSize));
    }

    private async Task RefreshScheduledCardAsync(Border card, PCInfo pc)
    {
        try { await RefreshOnePcFrameAsync(card, pc); }
        finally { _screenInFlight.Remove(pc.PcId); }
    }

    private static bool IsPcOccupied(PCInfo pc) => pc.IsEnabled &&
        (pc.Status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) || pc.Status.Equals("In Use", StringComparison.OrdinalIgnoreCase));

    private async Task EnsureMonitoringStartedAsync(int pcId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_thumbnailLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await EnsureMonitoringStartedAsync(pcId, timeout.Token);
    }

    private async Task EnsureMonitoringStartedAsync(int pcId, CancellationToken token)
    {
        // Renew periodically so a server restart does not leave the client cache saying "started".
        if (_screenStarted.TryGetValue(pcId, out var started) && DateTime.UtcNow - started < TimeSpan.FromSeconds(10)) return;
        using var response = await _httpClient.PostAsync($"api/ScreenMonitor/{pcId}/start", null, token);
        response.EnsureSuccessStatusCode();
        _screenMonitoringStarted.Add(pcId);
        _screenStarted[pcId] = DateTime.UtcNow;
    }

    private async Task RefreshOnePcFrameAsync(Border card, PCInfo pc)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_thumbnailLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await EnsureMonitoringStartedAsync(pc.PcId, timeout.Token);
            var metadata = await _httpClient.GetFromJsonAsync<ScreenFrameMetadata>($"api/ScreenMonitor/{pc.PcId}/meta", timeout.Token);
            if (metadata == null) return;
            _screenVersions.TryGetValue(pc.PcId, out long knownVersion);
            if (metadata.Version != knownVersion || !_latestScreenImages.ContainsKey(pc.PcId))
            {
                // Metadata is a hint; the response header is the authoritative image version.
                using var response = await _httpClient.GetAsync($"api/ScreenMonitor/{pc.PcId}", timeout.Token);
                response.EnsureSuccessStatusCode();
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                ImageSource? image = await Task.Run(() => DecodeFrozenImage(bytes), timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                if (image == null || !IsPcOccupied(pc) ||
                    (_screenOwners.TryGetValue(pc.PcId, out int? owner) && owner != pc.CurrentUserId)) return;
                _screenVersions[pc.PcId] = response.Headers.TryGetValues("X-SmartLab-Frame-Version", out var versions) &&
                    long.TryParse(versions.FirstOrDefault(), out long version) ? version : metadata.Version;
                _latestScreenImages[pc.PcId] = bytes;
                _decodedScreens[pc.PcId] = image;
                _screenUpdated[pc.PcId] = metadata.UpdatedAt;
                ApplyScreenImage(pc.PcId, image);
            }
            ApplyCachedScreenToCard(card, pc.PcId);
        }
        catch (OperationCanceledException) when (_thumbnailLifetime.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            ClientLog.Write("Warning", "PreviewTimeout", new { pcId = pc.PcId });
        }
        catch (Exception ex)
        {
            _screenStarted.Remove(pc.PcId);
            ClientLog.Write(ex is HttpRequestException ? "Warning" : "Error", "PreviewRefreshFailed", new { pcId = pc.PcId }, ex);
        }
        finally
        {
            if (!_thumbnailLifetime.IsCancellationRequested) SetPreviewAge(card, pc.PcId);
        }
    }

    private void SetPreviewAge(Border card, int pcId)
    {
        if (FindMonitorFrame(card) is not Border frame) return;
        double age = _screenUpdated.TryGetValue(pcId, out var updated) ? Math.Max(0, (DateTime.Now - updated).TotalSeconds) : double.PositiveInfinity;
        string text = double.IsPositiveInfinity(age) ? "No current preview" : $"Last updated: {age:0.0}s ago" + (age > 10 ? " — STALE" : "");
        frame.ToolTip = text;
        frame.Opacity = age > 10 ? 0.45 : 1;
        System.Windows.Automation.AutomationProperties.SetHelpText(frame, text);
    }

    private static ImageSource? DecodeFrozenImage(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            ClientLog.Write("Warning", "PreviewDecodeFailed", exception: ex);
            return null;
        }
    }

    private void ApplyScreenImage(int pcId, ImageSource imageSource)
    {
        var card = PcGrid.Children.OfType<Border>().FirstOrDefault(c => c.Tag is PCInfo pc && pc.PcId == pcId);
        if (card == null || card.Tag is not PCInfo current || !IsPcOccupied(current) ||
            (_screenOwners.TryGetValue(pcId, out int? owner) && owner != current.CurrentUserId)) return;
        var frame = FindMonitorFrame(card);
        if (frame == null) return;
        // Always resolve the current card: the lab grid can be rebuilt while a request is in flight.
        if (frame.Child is not Image image)
        {
            image = new Image { Stretch = Stretch.UniformToFill };
            frame.Child = image;
        }
        _screenImageControls[pcId] = image;
        image.Source = imageSource;
        SetPreviewAge(card, pcId);
    }

    private void ApplyCachedScreenToCard(Border card, int pcId)
    {
        if (card.Tag is PCInfo pc && (!IsPcOccupied(pc) ||
            (_screenOwners.TryGetValue(pcId, out int? owner) && owner != pc.CurrentUserId))) return;
        if (_decodedScreens.TryGetValue(pcId, out var image)) ApplyScreenImage(pcId, image);
    }

    private static Border? FindMonitorFrame(DependencyObject root) =>
        FindVisualChildren<Border>(root).FirstOrDefault(b => Math.Abs(b.Width - 74) < 0.5 && Math.Abs(b.Height - 47) < 0.5);

    private sealed class ScreenFrameMetadata
    {
        public int PcId { get; set; }
        public long Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
