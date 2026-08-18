using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - MEDIUM LIVE SCREEN VIEWER
    // STEP 31
    //
    // Uses the existing screen-monitoring pipeline.
    // The viewer is intentionally NOT full-screen.
    //
    // IMPORTANT:
    // - This is a NEW partial class file.
    // - Do not modify AdminDashboard.xaml.
    // - Do not modify the existing ScreenMonitoring file.
    // - No remote keyboard/mouse control.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _liveScreenViewerClassHandlerRegistered =
                RegisterLiveScreenViewerClassHandler();

        private static bool
            RegisterLiveScreenViewerClassHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(
                    LiveScreenViewerButtonClickClassHandler),
                true);

            return true;
        }

        private static void
            LiveScreenViewerButtonClickClassHandler(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not Button button ||
                !string.Equals(
                    button.Name,
                    "ViewFullScreenButton",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (Window.GetWindow(button)
                is not AdminDashboard dashboard)
            {
                return;
            }

            e.Handled = true;

            _ = dashboard.OpenMediumLiveScreenAsync();
        }

        // ==========================================================
        // OPEN MEDIUM LIVE VIEWER
        // ==========================================================

        private async Task
            OpenMediumLiveScreenAsync()
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Live Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            bool occupied =
                _selectedPc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                _selectedPc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);

            if (!occupied)
            {
                MessageBox.Show(
                    "The selected PC is not currently in use.",
                    "SmartLab - Live Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            try
            {
                await EnsureMonitoringStartedAsync(
                    _selectedPc.PcId);

                Border? card =
                    PcGrid.Children
                        .OfType<Border>()
                        .FirstOrDefault(
                            border =>
                                border.Tag is PCInfo pc &&
                                pc.PcId ==
                                    _selectedPc.PcId);

                if (card == null)
                {
                    MessageBox.Show(
                        "The selected PC card could not be found.",
                        "SmartLab - Live Screen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // Load the newest available frame before showing
                // the viewer.
                await RefreshOnePcFrameAsync(
                    card,
                    _selectedPc);

                if (!_latestScreenImages.TryGetValue(
                        _selectedPc.PcId,
                        out byte[]? imageBytes) ||
                    imageBytes.Length == 0)
                {
                    MessageBox.Show(
                        "No live screen frame is available yet.\n\n" +
                        "Wait for the PC preview to update, then try again.",
                        "SmartLab - Live Screen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                ImageSource? imageSource =
                    DecodeFrozenImage(
                        imageBytes);

                if (imageSource == null)
                {
                    MessageBox.Show(
                        "The latest screen frame could not be displayed.",
                        "SmartLab - Live Screen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                ShowMediumLiveScreenWindow(
                    _selectedPc,
                    card,
                    imageSource);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the live screen viewer.\n\n" +
                    ex.Message,
                    "SmartLab - Live Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ==========================================================
        // MEDIUM LIVE VIEWER WINDOW
        // ==========================================================

        private void ShowMediumLiveScreenWindow(
            PCInfo pc,
            Border card,
            ImageSource initialImage)
        {
            Window viewer =
                new Window
                {
                    Title =
                        $"SmartLab - Live Screen - {pc.PcNumber}",

                    Width =
                        1050,

                    Height =
                        700,

                    MinWidth =
                        800,

                    MinHeight =
                        520,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    Owner =
                        this,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                6,
                                12,
                                18)),

                    ResizeMode =
                        ResizeMode.CanResize,

                    ShowInTaskbar =
                        false
                };

            Grid root =
                new Grid
                {
                    Background =
                        Brushes.Black
                };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            // ------------------------------------------------------
            // HEADER
            // ------------------------------------------------------

            Border header =
                new Border
                {
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                11,
                                20,
                                27)),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                35,
                                51,
                                62)),

                    BorderThickness =
                        new Thickness(
                            0,
                            0,
                            0,
                            1),

                    Padding =
                        new Thickness(
                            14,
                            11,
                            14,
                            11)
                };

            Grid headerGrid =
                new Grid();

            headerGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            headerGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            TextBlock title =
                new TextBlock
                {
                    Text =
                        $"LIVE SCREEN • {pc.PcNumber}",

                    Foreground =
                        Brushes.White,

                    FontSize =
                        14,

                    FontWeight =
                        FontWeights.SemiBold,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            Grid.SetColumn(
                title,
                0);

            headerGrid.Children.Add(
                title);

            TextBlock status =
                new TextBlock
                {
                    Text =
                        "LIVE",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                58,
                                191,
                                120)),

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.Bold,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            Grid.SetColumn(
                status,
                1);

            headerGrid.Children.Add(
                status);

            header.Child =
                headerGrid;

            Grid.SetRow(
                header,
                0);

            root.Children.Add(
                header);

            // ------------------------------------------------------
            // SCREEN
            // ------------------------------------------------------

            Border screenBorder =
                new Border
                {
                    Background =
                        Brushes.Black,

                    Margin =
                        new Thickness(
                            12),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                40,
                                54,
                                64)),

                    BorderThickness =
                        new Thickness(1),

                    HorizontalAlignment =
                        HorizontalAlignment.Stretch,

                    VerticalAlignment =
                        VerticalAlignment.Stretch
                };

            Image liveImage =
                new Image
                {
                    Source =
                        initialImage,

                    Stretch =
                        Stretch.Uniform,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            screenBorder.Child =
                liveImage;

            Grid.SetRow(
                screenBorder,
                1);

            root.Children.Add(
                screenBorder);

            // ------------------------------------------------------
            // FOOTER
            // ------------------------------------------------------

            Border footer =
                new Border
                {
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                11,
                                20,
                                27)),

                    Padding =
                        new Thickness(
                            12,
                            8,
                            12,
                            8),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                35,
                                51,
                                62)),

                    BorderThickness =
                        new Thickness(
                            0,
                            1,
                            0,
                            0)
                };

            TextBlock footerText =
                new TextBlock
                {
                    Text =
                        "Screen preview updates approximately every second • ESC to close",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                139,
                                154,
                                165)),

                    FontSize =
                        11
                };

            footer.Child =
                footerText;

            Grid.SetRow(
                footer,
                2);

            root.Children.Add(
                footer);

            // ------------------------------------------------------
            // LIVE REFRESH TIMER
            // ------------------------------------------------------

            DispatcherTimer liveTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(
                            1000)
                };

            bool viewerClosing =
                false;

            bool refreshRunning =
                false;

            liveTimer.Tick += async (s, e) =>
            {
                if (viewerClosing ||
                    refreshRunning)
                {
                    return;
                }

                refreshRunning =
                    true;

                try
                {
                    // Use the existing monitoring pipeline.
                    await RefreshOnePcFrameAsync(
                        card,
                        pc);

                    if (_latestScreenImages.TryGetValue(
                            pc.PcId,
                            out byte[]? latestBytes) &&
                        latestBytes.Length > 0)
                    {
                        ImageSource? latestImage =
                            DecodeFrozenImage(
                                latestBytes);

                        if (latestImage != null)
                        {
                            liveImage.Source =
                                latestImage;
                        }
                    }
                }
                catch
                {
                    // Keep showing the last successful frame.
                }
                finally
                {
                    refreshRunning =
                        false;
                }
            };

            // ------------------------------------------------------
            // CLOSE
            // ------------------------------------------------------

            viewer.KeyDown +=
                (s, e) =>
                {
                    if (e.Key ==
                        Key.Escape)
                    {
                        viewerClosing =
                            true;

                        liveTimer.Stop();

                        viewer.Close();

                        e.Handled =
                            true;
                    }
                };

            viewer.Closed +=
                (s, e) =>
                {
                    viewerClosing =
                        true;

                    liveTimer.Stop();
                };

            viewer.Content =
                root;

            liveTimer.Start();

            viewer.ShowDialog();

            liveTimer.Stop();
        }
    }
}
