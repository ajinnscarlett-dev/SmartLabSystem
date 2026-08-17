using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - STABLE LIVE PC THUMBNAILS
    // STEP 26
    //
    // Root cause addressed:
    // The previous code treated an old LastSeen value as a reason
    // to stop screen monitoring. StopMonitoring clears the server's
    // latest frame. That can leave the Admin with an old cached image
    // while GET /meta returns 404.
    //
    // New rule:
    // - If a PC is IN USE/OCCUPIED, keep monitoring.
    // - Do NOT stop monitoring only because LastSeen is temporarily
    //   stale in the Admin's current PC list.
    // - Stop monitoring when the PC is no longer occupied.
    //
    // This avoids deleting the current frame because of a transient
    // heartbeat/dashboard refresh delay.
    // ==========================================================

    public partial class AdminDashboard
    {
        private DispatcherTimer? _screenThumbnailTimer;
        private bool _screenThumbnailRefreshRunning;
        private int _screenRefreshIndex;

        private readonly HashSet<int>
            _screenMonitoringStarted =
                new HashSet<int>();

        private readonly Dictionary<int, long>
            _screenVersions =
                new Dictionary<int, long>();

        private readonly Dictionary<int, byte[]>
            _latestScreenImages =
                new Dictionary<int, byte[]>();

        private readonly Dictionary<int, Image>
            _screenImageControls =
                new Dictionary<int, Image>();

        private static readonly TimeSpan
            ScreenRefreshInterval =
                TimeSpan.FromMilliseconds(1000);

        private void InitializeScreenMonitoring()
        {
            if (_screenThumbnailTimer != null)
            {
                return;
            }

            _screenThumbnailTimer =
                new DispatcherTimer
                {
                    Interval =
                        ScreenRefreshInterval
                };

            _screenThumbnailTimer.Tick +=
                ScreenThumbnailTimer_Tick;

            _screenThumbnailTimer.Start();

            _ = RefreshScreenMetadataAsync();
        }

        private async void ScreenThumbnailTimer_Tick(
            object? sender,
            EventArgs e)
        {
            await RefreshScreenMetadataAsync();
        }

        private async Task RefreshScreenMetadataAsync()
        {
            if (_screenThumbnailRefreshRunning ||
                !IsLoaded)
            {
                return;
            }

            _screenThumbnailRefreshRunning = true;

            try
            {
                List<(Border Card, PCInfo PC)>
                    visibleCards =
                        PcGrid.Children
                            .OfType<Border>()
                            .Where(
                                b => b.Tag is PCInfo)
                            .Select(
                                b => (
                                    b,
                                    (PCInfo)b.Tag
                                ))
                            .ToList();

                if (visibleCards.Count == 0)
                {
                    _screenRefreshIndex = 0;
                    return;
                }

                if (_screenRefreshIndex >=
                    visibleCards.Count)
                {
                    _screenRefreshIndex = 0;
                }

                // IMPORTANT:
                // Monitoring state now follows PC usage state,
                // not LastSeen freshness.
                await SynchronizeMonitoringStatesAsync(
                    visibleCards);

                int attempts =
                    visibleCards.Count;

                while (attempts-- > 0)
                {
                    var item =
                        visibleCards[
                            _screenRefreshIndex];

                    _screenRefreshIndex =
                        (_screenRefreshIndex + 1) %
                        visibleCards.Count;

                    if (!IsPcOccupied(item.PC))
                    {
                        continue;
                    }

                    await RefreshOnePcFrameAsync(
                        item.Card,
                        item.PC);

                    break;
                }
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"Screen preview error: {ex.Message}";
            }
            finally
            {
                _screenThumbnailRefreshRunning =
                    false;
            }
        }

        // ==========================================================
        // MONITORING STATE
        // ==========================================================

        private async Task
            SynchronizeMonitoringStatesAsync(
                List<(Border Card, PCInfo PC)>
                    visibleCards)
        {
            foreach (var item in visibleCards)
            {
                bool occupied =
                    IsPcOccupied(item.PC);

                if (occupied)
                {
                    // Do not require a fresh LastSeen value here.
                    // The Student client is the one uploading frames,
                    // and a temporary dashboard heartbeat delay should
                    // not cause us to delete the current frame.
                    await EnsureMonitoringStartedAsync(
                        item.PC.PcId);
                }
                else if (
                    _screenMonitoringStarted.Contains(
                        item.PC.PcId))
                {
                    await StopMonitoringAsync(
                        item.PC.PcId);
                }
            }
        }

        private static bool IsPcOccupied(
            PCInfo pc)
        {
            return
                pc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                pc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);
        }

        private async Task
            EnsureMonitoringStartedAsync(
                int pcId)
        {
            if (_screenMonitoringStarted.Contains(
                pcId))
            {
                return;
            }

            try
            {
                HttpResponseMessage response =
                    await _httpClient.PostAsync(
                        $"api/ScreenMonitor/{pcId}/start",
                        null);

                if (response.IsSuccessStatusCode)
                {
                    _screenMonitoringStarted.Add(
                        pcId);
                }
            }
            catch
            {
                // Retry on the next cycle.
            }
        }

        private async Task
            StopMonitoringAsync(
                int pcId)
        {
            try
            {
                HttpResponseMessage response =
                    await _httpClient.PostAsync(
                        $"api/ScreenMonitor/{pcId}/stop",
                        null);

                if (response.IsSuccessStatusCode)
                {
                    _screenMonitoringStarted.Remove(
                        pcId);

                    _screenVersions.Remove(
                        pcId);

                    _latestScreenImages.Remove(
                        pcId);

                    _screenImageControls.Remove(
                        pcId);
                }
            }
            catch
            {
                // Retry on the next cycle.
            }
        }

        // ==========================================================
        // GET LATEST FRAME
        // ==========================================================

        private async Task
            RefreshOnePcFrameAsync(
                Border card,
                PCInfo pc)
        {
            try
            {
                ScreenFrameMetadata? metadata =
                    await _httpClient
                        .GetFromJsonAsync<
                            ScreenFrameMetadata>(
                                $"api/ScreenMonitor/{pc.PcId}/meta");

                if (metadata == null)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                _screenVersions.TryGetValue(
                    pc.PcId,
                    out long knownVersion);

                if (metadata.Version <=
                    knownVersion)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                byte[] imageBytes =
                    await _httpClient.GetByteArrayAsync(
                        $"api/ScreenMonitor/{pc.PcId}" +
                        $"?version={knownVersion}");

                if (imageBytes.Length == 0)
                {
                    return;
                }

                ImageSource? image =
                    await Task.Run(
                        () =>
                            DecodeFrozenImage(
                                imageBytes));

                if (image == null)
                {
                    return;
                }

                _screenVersions[pc.PcId] =
                    metadata.Version;

                _latestScreenImages[pc.PcId] =
                    imageBytes;

                ApplyScreenImage(
                    pc.PcId,
                    image);
            }
            catch
            {
                // Keep the previous good frame.
                ApplyCachedScreenToCard(
                    card,
                    pc.PcId);
            }
        }

        // ==========================================================
        // IMAGE DECODE
        // ==========================================================

        private static ImageSource?
            DecodeFrozenImage(
                byte[] imageBytes)
        {
            try
            {
                BitmapImage bitmap =
                    new BitmapImage();

                using MemoryStream stream =
                    new MemoryStream(
                        imageBytes);

                bitmap.BeginInit();

                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmap.StreamSource =
                    stream;

                bitmap.EndInit();

                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // ==========================================================
        // DISPLAY NEW FRAME
        // ==========================================================

        private void ApplyScreenImage(
            int pcId,
            ImageSource imageSource)
        {
            if (!_screenImageControls.TryGetValue(
                pcId,
                out Image? imageControl))
            {
                Border? card =
                    PcGrid.Children
                        .OfType<Border>()
                        .FirstOrDefault(
                            border =>
                                border.Tag is PCInfo pc &&
                                pc.PcId == pcId);

                if (card == null)
                {
                    return;
                }

                Border? monitorFrame =
                    FindMonitorFrame(card);

                if (monitorFrame == null)
                {
                    return;
                }

                imageControl =
                    new Image
                    {
                        Stretch =
                            Stretch.UniformToFill,

                        HorizontalAlignment =
                            HorizontalAlignment.Stretch,

                        VerticalAlignment =
                            VerticalAlignment.Stretch
                    };

                _screenImageControls[pcId] =
                    imageControl;

                monitorFrame.Child =
                    imageControl;
            }

            imageControl.Source =
                imageSource;
        }

        // ==========================================================
        // DISPLAY CACHED FRAME
        // ==========================================================

        private void ApplyCachedScreenToCard(
            Border card,
            int pcId)
        {
            if (!_latestScreenImages.TryGetValue(
                pcId,
                out byte[]? imageBytes))
            {
                return;
            }

            ImageSource? image =
                DecodeFrozenImage(
                    imageBytes);

            if (image == null)
            {
                return;
            }

            ApplyScreenImage(
                pcId,
                image);
        }

        // ==========================================================
        // FIND MONITOR FRAME
        // ==========================================================

        private static Border? FindMonitorFrame(
            DependencyObject root)
        {
            return FindVisualChildren<Border>(
                    root)
                .FirstOrDefault(
                    border =>
                        Math.Abs(
                            border.Width - 74) < 0.5
                        &&
                        Math.Abs(
                            border.Height - 47) < 0.5);
        }

        // ==========================================================
        // VERSION METADATA MODEL
        // ==========================================================

        private sealed class ScreenFrameMetadata
        {
            public int PcId { get; set; }

            public long Version { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
