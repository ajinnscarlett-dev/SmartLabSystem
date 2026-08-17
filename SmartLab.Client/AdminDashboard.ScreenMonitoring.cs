using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - STABLE LIVE SCREEN REFRESH
    // ==========================================================
    //
    // STEP 24
    //
    // We are intentionally NOT downloading all PC images in parallel.
    // The previous parallel approach could keep several network/image
    // operations alive at the same time and make a WPF dashboard
    // progressively less responsive.
    //
    // New approach:
    // - One visible PC frame at a time.
    // - ~1.2 second refresh cadence.
    // - Per-request timeout.
    // - JPEG decode happens off the UI thread.
    // - Existing Image control is reused.
    // - Last good frame stays visible when a request fails.
    // - No PC grid rebuild.
    //
    // This favors stability and smoothness over maximum FPS.
    //
    // ==========================================================

    public partial class AdminDashboard
    {
        private DispatcherTimer? _screenThumbnailTimer;

        private bool _screenThumbnailRefreshRunning;

        private int _screenRefreshIndex;

        private readonly HashSet<int>
            _screenMonitoringStarted =
                new HashSet<int>();

        private readonly Dictionary<int, byte[]>
            _latestScreenImages =
                new Dictionary<int, byte[]>();

        private readonly Dictionary<int, Image>
            _screenImageControls =
                new Dictionary<int, Image>();

        private static readonly TimeSpan
            ScreenRefreshInterval =
                TimeSpan.FromMilliseconds(1200);

        private static readonly TimeSpan
            ScreenRequestTimeout =
                TimeSpan.FromMilliseconds(1500);

        // ==========================================================
        // INITIALIZE
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
                        ScreenRefreshInterval
                };

            _screenThumbnailTimer.Tick +=
                ScreenThumbnailTimer_Tick;

            _screenThumbnailTimer.Start();

            _ = RefreshOneScreenAsync();
        }

        // ==========================================================
        // TIMER
        // ==========================================================

        private async void ScreenThumbnailTimer_Tick(
            object? sender,
            EventArgs e)
        {
            await RefreshOneScreenAsync();
        }

        // ==========================================================
        // REFRESH ONE SCREEN
        // ==========================================================
        //
        // One frame only per cycle.
        // This prevents a burst of concurrent downloads/decodes.
        // ==========================================================

        private async Task RefreshOneScreenAsync()
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
                                b =>
                                    (
                                        b,
                                        (PCInfo)b.Tag
                                    ))
                            .ToList();

                if (visibleCards.Count == 0)
                {
                    _screenRefreshIndex = 0;
                    return;
                }

                // Make sure the index is still inside the current
                // visible-card range.
                if (_screenRefreshIndex >=
                    visibleCards.Count)
                {
                    _screenRefreshIndex = 0;
                }

                // Start/stop monitoring based on current state.
                await SynchronizeMonitoringStatesAsync(
                    visibleCards);

                // Find the next occupied + online PC.
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

                    if (!IsPcOccupied(item.PC) ||
                        !IsPcOnline(item.PC))
                    {
                        continue;
                    }

                    await DownloadAndApplyOneFrameAsync(
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
                _screenThumbnailRefreshRunning = false;
            }
        }

        // ==========================================================
        // SYNC MONITORING STATES
        // ==========================================================

        private async Task SynchronizeMonitoringStatesAsync(
            List<(Border Card, PCInfo PC)> visibleCards)
        {
            foreach (var item in visibleCards)
            {
                PCInfo pc =
                    item.PC;

                bool occupied =
                    IsPcOccupied(pc);

                bool online =
                    IsPcOnline(pc);

                if (occupied && online)
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
        }

        // ==========================================================
        // PC STATE
        // ==========================================================

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

        private static bool IsPcOnline(
            PCInfo pc)
        {
            if (!pc.LastSeen.HasValue)
            {
                return false;
            }

            return
                (DateTime.Now -
                 pc.LastSeen.Value).TotalSeconds <= 10;
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
                // Try again during a later cycle.
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

                    _screenImageControls.Remove(
                        pcId);
                }
            }
            catch
            {
                // Try again during a later cycle.
            }
        }

        // ==========================================================
        // DOWNLOAD ONE FRAME
        // ==========================================================

        private async Task
            DownloadAndApplyOneFrameAsync(
                Border card,
                PCInfo pc)
        {
            try
            {
                using CancellationTokenSource timeoutCts =
                    new CancellationTokenSource(
                        ScreenRequestTimeout);

                HttpResponseMessage response =
                    await _httpClient.GetAsync(
                        $"api/ScreenMonitor/{pc.PcId}",
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                byte[] imageBytes =
                    await response.Content
                        .ReadAsByteArrayAsync(
                            timeoutCts.Token);

                if (imageBytes.Length == 0)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                ImageSource? image =
                    await Task.Run(
                        () =>
                            DecodeFrozenImage(
                                imageBytes));

                if (image == null)
                {
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    return;
                }

                _latestScreenImages[
                    pc.PcId] =
                    imageBytes;

                ApplyScreenImage(
                    pc.PcId,
                    image);
            }
            catch
            {
                // A timeout/network hiccup must not freeze the
                // refresh loop. Keep the last successful frame.
                ApplyCachedScreenToCard(
                    card,
                    pc.PcId);
            }
        }

        // ==========================================================
        // DECODE OFF UI THREAD
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
        // APPLY NEW FRAME
        // ==========================================================

        private void ApplyScreenImage(
            int pcId,
            ImageSource imageSource)
        {
            Image? imageControl = null;

            if (!_screenImageControls.TryGetValue(
                pcId,
                out imageControl))
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
        // APPLY CACHED FRAME
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

            Border? monitorFrame =
                FindMonitorFrame(card);

            if (monitorFrame == null)
            {
                return;
            }

            Image imageControl =
                new Image
                {
                    Source = image,

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
        // RESULT-FREE IMPLEMENTATION
        // ==========================================================
        //
        // No Task.WhenAll here.
        // No parallel image decode.
        // No 500ms DispatcherTimer.
        // One frame per cycle, with timeout.
        //
        // ==========================================================
    }
}
