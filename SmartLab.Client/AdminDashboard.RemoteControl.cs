using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // Authenticated, COMLAB-authorized remote-control layer built on
    // the existing live-screen monitoring flow.
    public partial class AdminDashboard
    {
        private static readonly bool _remoteControlHandlerRegistered = RegisterRemoteControlHandler();

        private static bool RegisterRemoteControlHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(AdminDashboardLoadedForRemoteControl));
            return true;
        }

        private static void AdminDashboardLoadedForRemoteControl(object sender, RoutedEventArgs e)
        {
            if (sender is AdminDashboard dashboard)
                dashboard.BindExistingRemoteControlButton();
        }

        private void BindExistingRemoteControlButton()
        {
            if (FindName("RemoteControlButton") is not Button button)
                return;

            button.Click -= UnsupportedQuickActionButton_Click;
            button.Click -= RemoteControlButton_Click;
            button.Click += RemoteControlButton_Click;
        }

        private async void RemoteControlButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show("Select a PC first.", "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool occupied =
                _selectedPc.Status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) ||
                _selectedPc.Status.Equals("In Use", StringComparison.OrdinalIgnoreCase);

            if (!occupied || !_selectedPc.CurrentUserId.HasValue)
            {
                MessageBox.Show("A student must be logged in to this PC.", "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await StartRemoteControlSessionAsync(_selectedPc);
                await EnsureMonitoringStartedAsync(_selectedPc.PcId);

                Border? card = PcGrid.Children
                    .OfType<Border>()
                    .FirstOrDefault(border =>
                        border.Tag is PCInfo pc && pc.PcId == _selectedPc.PcId);

                if (card == null)
                {
                    MessageBox.Show("The selected PC card could not be found.", "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await StopRemoteControlSessionAsync(_selectedPc.PcId);
                    return;
                }

                await RefreshOnePcFrameAsync(card, _selectedPc);

                if (!_latestScreenImages.TryGetValue(_selectedPc.PcId, out byte[]? bytes) || bytes.Length == 0)
                {
                    MessageBox.Show("No live screen frame is available yet.", "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Information);
                    await StopRemoteControlSessionAsync(_selectedPc.PcId);
                    return;
                }

                ImageSource? initial = DecodeFrozenImage(bytes);
                if (initial == null)
                {
                    await StopRemoteControlSessionAsync(_selectedPc.PcId);
                    MessageBox.Show("Unable to decode the live frame.", "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ShowRemoteControlWindowAsync(_selectedPc, card, initial);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to start remote control.\n\n" + ex.Message, "SmartLab - Remote Control", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartRemoteControlSessionAsync(PCInfo pc)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/PCCommand/{pc.PcId}/remote-control/start", new { });
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(await response.Content.ReadAsStringAsync());

            PCCommandSimpleResponse? result = await response.Content.ReadFromJsonAsync<PCCommandSimpleResponse>();
            if (result == null)
                throw new InvalidOperationException("Remote control start command returned no result.");

            await WaitForRemoteCommandCompletionAsync(result.CommandId, "Remote control session start");
        }

        private async Task StopRemoteControlSessionAsync(int pcId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/PCCommand/{pcId}/remote-control/stop", new { });
                if (!response.IsSuccessStatusCode)
                    return;

                PCCommandSimpleResponse? result = await response.Content.ReadFromJsonAsync<PCCommandSimpleResponse>();
                if (result != null)
                    await WaitForRemoteCommandCompletionAsync(result.CommandId, "Remote control session stop");
            }
            catch
            {
                // Session cleanup must not crash the dashboard.
            }
        }

        private async Task WaitForRemoteCommandCompletionAsync(long commandId, string operation)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(150);

                var response = await _httpClient.GetAsync($"api/PCCommand/{commandId}");
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"{operation} status check failed.");

                PCCommandStatusResponse? result = await response.Content.ReadFromJsonAsync<PCCommandStatusResponse>();
                if (result == null)
                    continue;

                if (string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    return;

                if (string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(result.Result ?? $"{operation} failed.");
            }

            throw new TimeoutException($"{operation} timed out.");
        }

        private async Task ShowRemoteControlWindowAsync(PCInfo pc, Border card, ImageSource initialImage)
        {
            Window window = new Window
            {
                Title = $"SmartLab - Remote Control - {pc.PcNumber}",
                Width = 1200,
                Height = 760,
                MinWidth = 900,
                MinHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = Brushes.Black,
                ShowInTaskbar = true
            };

            Grid root = new Grid { Background = Brushes.Black };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border toolbar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 20, 28)),
                Padding = new Thickness(10)
            };

            StackPanel toolbarPanel = new StackPanel { Orientation = Orientation.Horizontal };
            toolbarPanel.Children.Add(new TextBlock
            {
                Text = $"REMOTE CONTROL • {pc.PcNumber}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 18, 0)
            });
            toolbarPanel.Children.Add(new TextBlock
            {
                Text = "Click inside the live screen to send a mouse click. Press a key to send one key press.",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 165, 174)),
                VerticalAlignment = VerticalAlignment.Center
            });
            toolbar.Child = toolbarPanel;
            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            Border imageBorder = new Border
            {
                Background = Brushes.Black,
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 51, 61)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(8),
                ClipToBounds = true
            };

            Image image = new Image
            {
                Source = initialImage,
                Stretch = Stretch.Uniform,
                Focusable = true
            };
            imageBorder.Child = image;
            Grid.SetRow(imageBorder, 1);
            root.Children.Add(imageBorder);

            Button stopButton = new Button
            {
                Content = "STOP REMOTE CONTROL",
                Width = 190,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(94, 42, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(138, 65, 65)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8)
            };
            Grid.SetRow(stopButton, 2);
            root.Children.Add(stopButton);

            DispatcherTimer? timer = null;
            bool closing = false;
            bool refreshRunning = false;

            async Task CloseRemoteWindowAsync()
            {
                if (closing)
                    return;

                closing = true;
                timer?.Stop();
                await StopRemoteControlSessionAsync(pc.PcId);
                window.Close();
            }

            stopButton.Click += async (s, e) => await CloseRemoteWindowAsync();

            window.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    await CloseRemoteWindowAsync();
                    e.Handled = true;
                    return;
                }

                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                int vk = KeyInterop.VirtualKeyFromKey(e.Key);
                if (vk <= 0 || vk > 255)
                {
                    e.Handled = true;
                    return;
                }

                await SendRemoteKeyAsync(pc.PcId, vk);
                e.Handled = true;
            };

            image.MouseLeftButtonDown += async (s, e) =>
            {
                if (closing)
                    return;

                (double x, double y) = CalculateNormalizedImagePoint(image, e.GetPosition(image));
                await SendRemoteMouseClickAsync(pc.PcId, x, y, "LEFT");
                image.Focus();
                e.Handled = true;
            };

            image.MouseRightButtonDown += async (s, e) =>
            {
                if (closing)
                    return;

                (double x, double y) = CalculateNormalizedImagePoint(image, e.GetPosition(image));
                await SendRemoteMouseClickAsync(pc.PcId, x, y, "RIGHT");
                image.Focus();
                e.Handled = true;
            };

            window.Closed += async (s, e) =>
            {
                if (closing)
                    return;

                closing = true;
                timer?.Stop();
                await StopRemoteControlSessionAsync(pc.PcId);
            };

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += async (s, e) =>
            {
                if (closing || refreshRunning)
                    return;

                refreshRunning = true;
                try
                {
                    await RefreshOnePcFrameAsync(card, pc);
                    if (_latestScreenImages.TryGetValue(pc.PcId, out byte[]? latest) && latest.Length > 0)
                    {
                        ImageSource? latestImage = DecodeFrozenImage(latest);
                        if (latestImage != null)
                            image.Source = latestImage;
                    }
                }
                catch
                {
                    // Keep the last good frame.
                }
                finally
                {
                    refreshRunning = false;
                }
            };

            window.Content = root;
            timer.Start();
            window.ShowDialog();
            timer.Stop();
        }

        private static (double X, double Y) CalculateNormalizedImagePoint(Image image, Point position)
        {
            if (image.Source is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                return (0.5, 0.5);

            double actualWidth = Math.Max(1, image.ActualWidth);
            double actualHeight = Math.Max(1, image.ActualHeight);
            double sourceWidth = bitmap.PixelWidth;
            double sourceHeight = bitmap.PixelHeight;
            double scale = Math.Min(actualWidth / sourceWidth, actualHeight / sourceHeight);
            double displayWidth = sourceWidth * scale;
            double displayHeight = sourceHeight * scale;
            double offsetX = (actualWidth - displayWidth) / 2.0;
            double offsetY = (actualHeight - displayHeight) / 2.0;
            double sourceX = (position.X - offsetX) / scale;
            double sourceY = (position.Y - offsetY) / scale;

            return (
                Math.Clamp(sourceX / sourceWidth, 0, 1),
                Math.Clamp(sourceY / sourceHeight, 0, 1));
        }

        private async Task SendRemoteKeyAsync(int pcId, int keyCode)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"api/PCCommand/{pcId}/remote-control/key",
                    new { KeyCode = keyCode });

                if (response.IsSuccessStatusCode)
                    await WaitForRemoteInputCompletionAsync(await GetCommandIdFromResponseAsync(response));
            }
            catch
            {
                // Ignore individual remote input failures.
            }
        }

        private async Task SendRemoteMouseClickAsync(int pcId, double x, double y, string button)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"api/PCCommand/{pcId}/remote-control/mouse-click",
                    new { X = x, Y = y, Button = button });

                if (!response.IsSuccessStatusCode)
                    return;

                await WaitForRemoteInputCompletionAsync(await GetCommandIdFromResponseAsync(response));
            }
            catch
            {
                // Ignore individual remote input failures.
            }
        }

        private async Task WaitForRemoteInputCompletionAsync(long commandId)
        {
            try
            {
                await WaitForRemoteCommandCompletionAsync(commandId, "Remote input");
            }
            catch
            {
                // Ignore individual input completion failures.
            }
        }

        private async Task<long> GetCommandIdFromResponseAsync(HttpResponseMessage response)
        {
            PCCommandSimpleResponse? result = await response.Content.ReadFromJsonAsync<PCCommandSimpleResponse>();
            if (result == null)
                throw new InvalidOperationException("Remote input command returned no command ID.");

            return result.CommandId;
        }

        private sealed class PCCommandStatusResponse
        {
            public long CommandId { get; set; }
            public int PCId { get; set; }
            public string? CommandType { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? Result { get; set; }
        }
    }
}