using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - LIVE PC THUMBNAILS
    // ==========================================================
    //
    // Screen frames are cached in memory so the 5-second PC status
    // refresh does not make the actual thumbnail disappear.
    //
    // The PC grid is also rebuilt only when PC identity/status changes.
    //
    // ==========================================================

    public partial class AdminDashboard
    {
        private DispatcherTimer? _screenThumbnailTimer;

        private bool _screenThumbnailRefreshRunning;

        private readonly HashSet<int>
            _screenMonitoringStarted =
                new HashSet<int>();

        private readonly Dictionary<int, byte[]>
            _latestScreenImages =
                new Dictionary<int, byte[]>();

        // ==========================================================
        // INITIALIZE SCREEN MONITORING
        // ==========================================================

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
                        TimeSpan.FromSeconds(3)
                };

            _screenThumbnailTimer.Tick +=
                ScreenThumbnailTimer_Tick;

            _screenThumbnailTimer.Start();

            _ = RefreshScreenThumbnailsAsync();
        }

        // ==========================================================
        // TIMER
        // ==========================================================

        private async void ScreenThumbnailTimer_Tick(
            object? sender,
            EventArgs e)
        {
            await RefreshScreenThumbnailsAsync();
        }

        // ==========================================================
        // REFRESH SCREEN THUMBNAILS
        // ==========================================================

        private async Task RefreshScreenThumbnailsAsync()
        {
            if (_screenThumbnailRefreshRunning ||
                !IsLoaded)
            {
                return;
            }

            _screenThumbnailRefreshRunning = true;

            try
            {
                var cards =
                    PcGrid.Children
                        .OfType<Border>()
                        .Where(
                            b => b.Tag is PCInfo)
                        .ToList();

                if (cards.Count == 0)
                {
                    return;
                }

                List<PCInfo> pcs =
                    cards
                        .Select(
                            b => (PCInfo)b.Tag)
                        .ToList();

                // --------------------------------------------------
                // START / STOP MONITORING
                // --------------------------------------------------

                foreach (PCInfo pc in pcs)
                {
                    bool occupied =
                        pc.Status.Equals(
                            "Occupied",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        pc.Status.Equals(
                            "In Use",
                            StringComparison.OrdinalIgnoreCase);

                    bool recentHeartbeat =
                        pc.LastSeen.HasValue &&
                        (DateTime.Now -
                         pc.LastSeen.Value).TotalSeconds <= 10;

                    if (occupied && recentHeartbeat)
                    {
                        await EnsureMonitoringStartedAsync(
                            pc.PcId);
                    }
                    else if (
                        _screenMonitoringStarted.Contains(
                            pc.PcId))
                    {
                        await StopMonitoringAsync(
                            pc.PcId);
                    }
                }

                // --------------------------------------------------
                // GET LATEST FRAMES
                // --------------------------------------------------

                foreach (Border card in cards)
                {
                    if (card.Tag is not PCInfo pc)
                    {
                        continue;
                    }

                    bool occupied =
                        pc.Status.Equals(
                            "Occupied",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        pc.Status.Equals(
                            "In Use",
                            StringComparison.OrdinalIgnoreCase);

                    bool recentHeartbeat =
                        pc.LastSeen.HasValue &&
                        (DateTime.Now -
                         pc.LastSeen.Value).TotalSeconds <= 10;

                    if (!occupied || !recentHeartbeat)
                    {
                        continue;
                    }

                    await UpdateCardWithLatestScreenAsync(
                        card,
                        pc);
                }
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"Screen preview error: {ex.Message}";
            }
            finally
            {
                _screenThumbnailRefreshRunning = false;
            }
        }

        // ==========================================================
        // START MONITORING
        // ==========================================================

        private async Task EnsureMonitoringStartedAsync(
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
                // Retry on the next refresh cycle.
            }
        }

        // ==========================================================
        // STOP MONITORING
        // ==========================================================

        private async Task StopMonitoringAsync(
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

                    _latestScreenImages.Remove(
                        pcId);
                }
            }
            catch
            {
                // Retry on the next refresh cycle.
            }
        }

        // ==========================================================
        // GET LATEST SCREEN
        // ==========================================================

        private async Task
            UpdateCardWithLatestScreenAsync(
                Border card,
                PCInfo pc)
        {
            try
            {
                HttpResponseMessage response =
                    await _httpClient.GetAsync(
                        $"api/ScreenMonitor/{pc.PcId}");

                if (!response.IsSuccessStatusCode)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                byte[] imageBytes =
                    await response.Content
                        .ReadAsByteArrayAsync();

                if (imageBytes.Length == 0)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                _latestScreenImages[pc.PcId] =
                    imageBytes;

                ApplyCachedScreenToCard(
                    card,
                    pc.PcId);
            }
            catch
            {
                // Keep the most recent successfully downloaded
                // screenshot visible.
                ApplyCachedScreenToCard(
                    card,
                    pc.PcId);
            }
        }

        // ==========================================================
        // APPLY CACHED SCREEN TO CARD
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

            Border? monitorFrame =
                FindMonitorFrame(card);

            if (monitorFrame == null)
            {
                return;
            }

            monitorFrame.Child =
                CreateScreenImage(
                    imageBytes);
        }

        // ==========================================================
        // CREATE IMAGE
        // ==========================================================

        private static Image CreateScreenImage(
            byte[] imageBytes)
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

            return new Image
            {
                Source = bitmap,

                Stretch =
                    Stretch.UniformToFill,

                HorizontalAlignment =
                    HorizontalAlignment.Stretch,

                VerticalAlignment =
                    VerticalAlignment.Stretch
            };
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
    }
}
