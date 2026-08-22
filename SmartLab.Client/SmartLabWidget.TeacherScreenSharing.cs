using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class SmartLabWidget
    {
        private DispatcherTimer?
            _teacherScreenShareTimer;

        private bool
            _teacherScreenSharePolling;

        private Window?
            _teacherScreenShareWindow;

        private System.Windows.Controls.Image?
            _teacherScreenShareImage;

        private long
            _lastTeacherScreenFrameVersion;

        private static readonly bool
            _teacherScreenShareBootstrap =
                RegisterTeacherScreenShareHandler();

        private static bool
            RegisterTeacherScreenShareHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(SmartLabWidget),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    SmartLabWidgetTeacherScreenLoaded));

            EventManager.RegisterClassHandler(
                typeof(SmartLabWidget),
                FrameworkElement.UnloadedEvent,
                new RoutedEventHandler(
                    SmartLabWidgetTeacherScreenUnloaded));

            return true;
        }

        private static void
            SmartLabWidgetTeacherScreenLoaded(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.InitializeTeacherScreenSharePolling();
            }
        }

        private static void
            SmartLabWidgetTeacherScreenUnloaded(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.StopTeacherScreenSharePolling();
            }
        }

        private void
            InitializeTeacherScreenSharePolling()
        {
            if (_teacherScreenShareTimer != null)
            {
                return;
            }

            _teacherScreenShareTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(1)
                };

            _teacherScreenShareTimer.Tick +=
                TeacherScreenShareTimer_Tick;

            _teacherScreenShareTimer.Start();

            _ =
                PollTeacherScreenShareAsync();
        }

        private async void
            TeacherScreenShareTimer_Tick(
                object? sender,
                EventArgs e)
        {
            await PollTeacherScreenShareAsync();
        }

        private async Task
            PollTeacherScreenShareAsync()
        {
            if (_isLoggingOut ||
                _teacherScreenSharePolling)
            {
                return;
            }

            _teacherScreenSharePolling =
                true;

            try
            {
                using HttpResponseMessage response =
                    await _httpClient.GetAsync(
                        "api/TeacherScreenShare/frame");

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.NoContent)
                {
                    CloseTeacherScreenShareWindow();
                    return;
                }

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.NotFound)
                {
                    CloseTeacherScreenShareWindow();
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                string? versionHeader =
                    response.Headers.TryGetValues(
                        "X-SmartLab-Teacher-Frame-Version",
                        out System.Collections.Generic.IEnumerable<string>?
                            versions)
                        ? System.Linq.Enumerable.FirstOrDefault(
                            versions)
                        : null;

                long.TryParse(
                    versionHeader,
                    out long version);

                if (version > 0 &&
                    version <=
                        _lastTeacherScreenFrameVersion)
                {
                    return;
                }

                byte[] image =
                    await response.Content
                        .ReadAsByteArrayAsync();

                if (image.Length == 0)
                {
                    return;
                }

                _lastTeacherScreenFrameVersion =
                    version;

                ShowOrUpdateTeacherScreenShare(
                    image);
            }
            catch
            {
                // The widget remains usable if the server/share
                // is temporarily unavailable.
            }
            finally
            {
                _teacherScreenSharePolling =
                    false;
            }
        }

        private void
            ShowOrUpdateTeacherScreenShare(
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

            if (_teacherScreenShareWindow ==
                null)
            {
                _teacherScreenShareWindow =
                    new Window
                    {
                        Title =
                            "SmartLab - Teacher Screen",

                        Width = 1050,
                        Height = 650,

                        MinWidth = 700,
                        MinHeight = 450,

                        WindowStartupLocation =
                            WindowStartupLocation.CenterScreen,

                        Background =
                            System.Windows.Media.Brushes.Black,

                        ResizeMode =
                            ResizeMode.CanResize,

                        ShowInTaskbar =
                            true,

                        Topmost = false
                    };

                Grid root =
                    new Grid
                    {
                        Background =
                            System.Windows.Media.Brushes.Black
                    };

                _teacherScreenShareImage =
                    new System.Windows.Controls.Image
                    {
                        Stretch =
                            System.Windows.Media.Stretch.Uniform
                    };

                root.Children.Add(
                    _teacherScreenShareImage);

                _teacherScreenShareWindow.Content =
                    root;

                _teacherScreenShareWindow.Closed +=
                    (s, e) =>
                    {
                        _teacherScreenShareWindow =
                            null;

                        _teacherScreenShareImage =
                            null;
                    };

                _teacherScreenShareWindow.Show();
            }

            if (_teacherScreenShareImage != null)
            {   
                _teacherScreenShareImage.Source =
                    bitmap;
            }

            if (_teacherScreenShareWindow != null &&
                !_teacherScreenShareWindow.IsVisible)
            {
                _teacherScreenShareWindow.Show();
            }

            _teacherScreenShareWindow?.Activate();
        }

        private void
            CloseTeacherScreenShareWindow()
        {
            try
            {
                _teacherScreenShareWindow?.Close();
            }
            catch
            {
            }

            _teacherScreenShareWindow =
                null;

            _teacherScreenShareImage =
                null;

            _lastTeacherScreenFrameVersion =
                0;
        }

        private void
            StopTeacherScreenSharePolling()
        {
            _teacherScreenShareTimer?.Stop();

            _teacherScreenShareTimer =
                null;

            CloseTeacherScreenShareWindow();
        }
    }
}
