using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class LiveScreenWindow : Window
    {
        // ==========================================
        // PC INFORMATION
        // ==========================================

        private readonly int _pcId;

        private readonly string _pcNumber;


        // ==========================================
        // HTTP CLIENT
        // ==========================================

        private readonly HttpClient _httpClient;


        // ==========================================
        // SCREEN REFRESH TIMER
        // ==========================================

        private readonly DispatcherTimer _screenTimer;


        // ==========================================
        // MONITORING STATE
        // ==========================================

        private bool _monitoring;


        // Prevent multiple screen requests
        // from running at the same time.

        private bool _loadingScreen;


        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public LiveScreenWindow(
            int pcId,
            string pcNumber)
        {
            InitializeComponent();


            _pcId = pcId;

            _pcNumber = pcNumber;


            // ==========================================
            // HTTP CLIENT
            // ==========================================

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri(
                        SmartLabServerConfig.BaseUrl
                    )
            };


            // ==========================================
            // DISPLAY PC
            // ==========================================

            PcText.Text =
                $"PC: {_pcNumber}";


            StatusText.Text =
                "Monitoring: OFF";


            StatusText.Foreground =
                System.Windows.Media.Brushes.DarkRed;


            StartStopButton.Content =
                "START MONITORING";


            NoScreenPanel.Visibility =
                Visibility.Visible;


            // ==========================================
            // SCREEN TIMER
            // ==========================================

            _screenTimer =
                new DispatcherTimer
                {
                    // Get latest screen every second
                    Interval =
                        TimeSpan.FromSeconds(1)
                };


            _screenTimer.Tick +=
                ScreenTimer_Tick;
        }


        // ==========================================
        // START / STOP BUTTON
        // ==========================================

        private async void StartStopButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_monitoring)
            {
                await StartMonitoring();
            }
            else
            {
                await StopMonitoring();
            }
        }


        // ==========================================
        // START MONITORING
        // ==========================================

        private async Task StartMonitoring()
        {
            try
            {
                StartStopButton.IsEnabled =
                    false;


                StartStopButton.Content =
                    "STARTING...";


                // ==========================================
                // ENABLE MONITORING ON SERVER
                // ==========================================

                var response =
                    await _httpClient.PostAsync(
                        $"api/ScreenMonitor/{_pcId}/start",
                        null
                    );


                // ==========================================
                // START FAILED
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    string error =
                        await response.Content
                            .ReadAsStringAsync();


                    MessageBox.Show(
                        "Unable to start monitoring.\n\n" +
                        error,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );


                    return;
                }


                // ==========================================
                // MONITORING ACTIVE
                // ==========================================

                _monitoring = true;


                StatusText.Text =
                    "Monitoring: ON";


                StatusText.Foreground =
                    System.Windows.Media.Brushes.Green;


                StartStopButton.Content =
                    "STOP MONITORING";


                NoScreenPanel.Visibility =
                    Visibility.Visible;


                // ==========================================
                // START SCREEN REFRESH
                // ==========================================

                _screenTimer.Start();


                // Try immediately
                await LoadScreen();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Connection error.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                StartStopButton.IsEnabled =
                    true;
            }
        }


        // ==========================================
        // STOP MONITORING
        // ==========================================

        private async Task StopMonitoring()
        {
            try
            {
                StartStopButton.IsEnabled =
                    false;


                StartStopButton.Content =
                    "STOPPING...";


                // ==========================================
                // STOP SCREEN TIMER
                // ==========================================

                _screenTimer.Stop();


                // ==========================================
                // DISABLE MONITORING ON SERVER
                // ==========================================

                var response =
                    await _httpClient.PostAsync(
                        $"api/ScreenMonitor/{_pcId}/stop",
                        null
                    );


                // ==========================================
                // STOP FAILED
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    string error =
                        await response.Content
                            .ReadAsStringAsync();


                    // Monitoring may still be active,
                    // so resume the timer.

                    _screenTimer.Start();


                    MessageBox.Show(
                        "Unable to stop monitoring.\n\n" +
                        error,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );


                    return;
                }


                // ==========================================
                // MONITORING STOPPED
                // ==========================================

                _monitoring = false;


                StatusText.Text =
                    "Monitoring: OFF";


                StatusText.Foreground =
                    System.Windows.Media.Brushes.DarkRed;


                StartStopButton.Content =
                    "START MONITORING";


                // Remove current image

                ScreenImage.Source =
                    null;


                NoScreenPanel.Visibility =
                    Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Resume monitoring timer
                // because stop request failed.

                _screenTimer.Start();


                MessageBox.Show(
                    "Connection error.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                StartStopButton.IsEnabled =
                    true;
            }
        }


        // ==========================================
        // SCREEN TIMER
        // ==========================================

        private async void ScreenTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (!_monitoring)
            {
                return;
            }


            await LoadScreen();
        }


        // ==========================================
        // LOAD LATEST SCREEN
        // ==========================================

        private async Task LoadScreen()
        {
            // Prevent overlapping requests.

            if (_loadingScreen)
            {
                return;
            }


            _loadingScreen = true;


            try
            {
                // ==========================================
                // GET LATEST SCREEN FROM SERVER
                // ==========================================

                var response =
                    await _httpClient.GetAsync(
                        $"api/ScreenMonitor/{_pcId}"
                    );


                // ==========================================
                // NO SCREEN AVAILABLE
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    NoScreenPanel.Visibility =
                        Visibility.Visible;


                    return;
                }


                // ==========================================
                // READ JPEG DATA
                // ==========================================

                byte[] imageBytes =
                    await response.Content
                        .ReadAsByteArrayAsync();


                if (imageBytes.Length == 0)
                {
                    NoScreenPanel.Visibility =
                        Visibility.Visible;


                    return;
                }


                // ==========================================
                // CONVERT JPEG TO BITMAP IMAGE
                // ==========================================

                using MemoryStream stream =
                    new MemoryStream(
                        imageBytes
                    );


                BitmapImage bitmap =
                    new BitmapImage();


                bitmap.BeginInit();


                // Important:
                // Load image completely into memory.

                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;


                bitmap.StreamSource =
                    stream;


                bitmap.EndInit();


                bitmap.Freeze();


                // ==========================================
                // DISPLAY SCREEN
                // ==========================================

                ScreenImage.Source =
                    bitmap;


                NoScreenPanel.Visibility =
                    Visibility.Collapsed;
            }
            catch
            {
                // Student PC or server may temporarily
                // be unavailable.

                // Do not crash the admin dashboard.
            }
            finally
            {
                _loadingScreen = false;
            }
        }


        // ==========================================
        // CLOSE BUTTON
        // ==========================================

        private async void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_monitoring)
            {
                await StopMonitoring();
            }


            Close();
        }


        // ==========================================
        // WINDOW CLOSED
        // ==========================================

        protected override void OnClosed(
            EventArgs e)
        {
            // Stop timer

            _screenTimer.Stop();


            // Dispose HTTP client

            _httpClient.Dispose();


            base.OnClosed(e);
        }
    }
}