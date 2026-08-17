using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class SmartLabWidget : Window
    {
        private readonly string _username;
        private readonly int _userId;
        private readonly int _pcId;
        private readonly string _pcNumber;

        private readonly HttpClient _httpClient;

        // ==========================================
        // HEARTBEAT
        // ==========================================

        private readonly DispatcherTimer _heartbeatTimer;


        // ==========================================
        // SCREEN MONITORING
        // ==========================================

        private readonly DispatcherTimer _screenMonitorTimer;

        private readonly ScreenCaptureService _screenCaptureService;

        private bool _screenMonitoringActive = false;

        private bool _screenUploading = false;


        // ==========================================
        // LOGOUT
        // ==========================================

        private bool _isLoggingOut;


        // ==========================================
        // STUDENT NOTIFICATIONS
        // ==========================================

        private readonly DispatcherTimer _notificationTimer;

        private bool _announcementStateInitialized;

        private int _lastAnnouncementId = 0;

        private int _unreadNotificationCount = 0;

        private readonly List<StudentNotification> _notifications =
            new List<StudentNotification>();


        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public SmartLabWidget(
            string username,
            string pcNumber,
            int userId,
            int pcId)
        {
            InitializeComponent();

            _username = username;
            _userId = userId;
            _pcId = pcId;
            _pcNumber = pcNumber;


            // ==========================================
            // HTTP CLIENT
            // ==========================================

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri(
                        "https://localhost:7277/"
                    )
            };

            // ==========================================
            // AUTHENTICATED HTTP SESSION
            // ==========================================

            AuthSession.Apply(_httpClient);


            // ==========================================
            // SCREEN CAPTURE SERVICE
            // ==========================================

            _screenCaptureService =
                new ScreenCaptureService();


            // ==========================================
            // DISPLAY USER / PC
            // ==========================================

            UsernameText.Text =
                _username;

            PcNumberText.Text =
                _pcNumber;


            // ==========================================
            // POSITION WIDGET
            // ==========================================

            Left =
                SystemParameters.WorkArea.Right
                - Width
                - 20;

            Top =
                SystemParameters.WorkArea.Top
                + 20;


            // ==========================================
            // HEARTBEAT TIMER
            // ==========================================

            _heartbeatTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(5)
                };

            _heartbeatTimer.Tick +=
                HeartbeatTimer_Tick;


            // ==========================================
            // START HEARTBEAT
            // ==========================================

            _heartbeatTimer.Start();


            // ==========================================
            // FIRST HEARTBEAT
            // ==========================================

            _ = SendHeartbeat();


            // ==========================================
            // SCREEN MONITOR TIMER
            // ==========================================

            _screenMonitorTimer =
                new DispatcherTimer
                {
                    // Check monitoring every 2 seconds
                    Interval =
                        TimeSpan.FromSeconds(2)
                };

            _screenMonitorTimer.Tick +=
                ScreenMonitorTimer_Tick;


            // ==========================================
            // START SCREEN MONITOR CHECK
            // ==========================================

            _screenMonitorTimer.Start();


            // ==========================================
            // NOTIFICATION TIMER
            // ==========================================

            _notificationTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(5)
                };

            _notificationTimer.Tick +=
                NotificationTimer_Tick;

            _notificationTimer.Start();

            _ = LoadAnnouncements();
        }


        // ==========================================
        // MOVE WIDGET
        // ==========================================

        private void Window_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ButtonState ==
                MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        // ==========================================
        // HEARTBEAT TIMER
        // ==========================================

        private async void HeartbeatTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_isLoggingOut)
            {
                return;
            }

            await SendHeartbeat();
        }


        // ==========================================
        // SEND HEARTBEAT
        // ==========================================

        private async Task SendHeartbeat()
        {
            if (_isLoggingOut)
            {
                return;
            }

            try
            {
                var response =
                    await _httpClient.PostAsync(
                        $"api/PC/{_pcId}/heartbeat",
                        null
                    );

                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                // Server updates LastSeen.
            }
            catch
            {
                // Do not close SmartLab.
                // Server-side monitor handles stale PCs.
            }
        }


        // ==========================================
        // SCREEN MONITOR TIMER
        // ==========================================

        private async void ScreenMonitorTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_isLoggingOut)
            {
                return;
            }

            await CheckScreenMonitoring();
        }


        // ==========================================
        // CHECK SCREEN MONITORING STATUS
        // ==========================================

        private async Task CheckScreenMonitoring()
        {
            try
            {
                var response =
                    await _httpClient.GetAsync(
                        $"api/ScreenMonitor/{_pcId}/status"
                    );


                if (!response.IsSuccessStatusCode)
                {
                    return;
                }


                var status =
                    await response.Content
                        .ReadFromJsonAsync<ScreenMonitoringStatus>();


                if (status == null)
                {
                    return;
                }


                // ==========================================
                // MONITORING STARTED
                // ==========================================

                if (status.Monitoring &&
                    !_screenMonitoringActive)
                {
                    _screenMonitoringActive = true;


                    Title =
                        "SmartLab - Screen Monitoring ACTIVE";


                    // Send first screenshot immediately.

                    await UploadCurrentScreen();
                }


                // ==========================================
                // MONITORING STOPPED
                // ==========================================

                else if (!status.Monitoring &&
                         _screenMonitoringActive)
                {
                    _screenMonitoringActive = false;


                    Title =
                        "SmartLab";


                    await RemoveScreenFromServer();
                }


                // ==========================================
                // MONITORING ACTIVE
                // ==========================================

                if (_screenMonitoringActive)
                {
                    await UploadCurrentScreen();
                }
            }
            catch
            {
                // Do not interrupt student's session.
            }
        }


        // ==========================================
        // CAPTURE AND UPLOAD SCREEN
        // ==========================================

        private async Task UploadCurrentScreen()
        {
            if (_screenUploading)
            {
                return;
            }


            if (!_screenMonitoringActive)
            {
                return;
            }


            _screenUploading = true;


            try
            {
                // ==========================================
                // CAPTURE SCREEN
                // ==========================================

                byte[] screenImage =
                    await Task.Run(
                        () =>
                            _screenCaptureService
                                .CaptureScreen()
                    );


                if (screenImage.Length == 0)
                {
                    return;
                }


                // ==========================================
                // SEND RAW JPEG
                // ==========================================

                using ByteArrayContent content =
                    new ByteArrayContent(
                        screenImage
                    );


                content.Headers.ContentType =
                    new MediaTypeHeaderValue(
                        "image/jpeg"
                    );


                var response =
                    await _httpClient.PostAsync(
                        $"api/ScreenMonitor/{_pcId}",
                        content
                    );


                // ==========================================
                // MONITORING NOT ENABLED
                // ==========================================

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.Forbidden)
                {
                    _screenMonitoringActive =
                        false;


                    Title =
                        "SmartLab";
                }


                // ==========================================
                // OTHER SERVER ERROR
                // ==========================================

                else if (!response.IsSuccessStatusCode)
                {
                    // Keep monitoring active.
                    // Next cycle will retry.

                    return;
                }
            }
            catch
            {
                // Do not close SmartLab if
                // screen upload temporarily fails.
            }
            finally
            {
                _screenUploading = false;
            }
        }


        // ==========================================
        // REMOVE SCREEN FROM SERVER
        // ==========================================

        private async Task RemoveScreenFromServer()
        {
            try
            {
                await _httpClient.DeleteAsync(
                    $"api/ScreenMonitor/{_pcId}"
                );
            }
            catch
            {
                // Ignore temporary server errors.
            }
        }


        // ==========================================
        // LOGOUT / RELEASE PC
        // ==========================================

        private async void LogoutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isLoggingOut)
            {
                return;
            }


            _isLoggingOut = true;


            LogoutButton.IsEnabled =
                false;

            LogoutButton.Content =
                "Logging out...";


            // ==========================================
            // STOP TIMERS
            // ==========================================

            _heartbeatTimer.Stop();

            _screenMonitorTimer.Stop();


            try
            {
                // ==========================================
                // STOP SCREEN MONITORING
                // ==========================================

                if (_screenMonitoringActive)
                {
                    await RemoveScreenFromServer();

                    _screenMonitoringActive =
                        false;
                }


                // ==========================================
                // RELEASE PC
                // ==========================================

                var response =
                    await _httpClient.PostAsync(
                        $"api/PC/release/{_userId}",
                        null
                    );


                var responseText =
                    await response.Content
                        .ReadAsStringAsync();


                // ==========================================
                // RELEASE FAILED
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    _isLoggingOut = false;


                    _heartbeatTimer.Start();

                    _screenMonitorTimer.Start();


                    MessageBox.Show(
                        "Unable to release the PC.\n\n" +
                        responseText,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );


                    LogoutButton.IsEnabled =
                        true;

                    LogoutButton.Content =
                        "Logout";


                    return;
                }


                // ==========================================
                // OPEN LOGIN AGAIN
                // ==========================================

                // ==========================================
                // CLEAR AUTHENTICATED SESSION
                // ==========================================

                AuthSession.Clear();



                MainWindow loginWindow =
                    new MainWindow();

                loginWindow.Show();


                // ==========================================
                // CLOSE WIDGET
                // ==========================================

                Close();
            }
            catch (Exception ex)
            {
                _isLoggingOut = false;


                _heartbeatTimer.Start();

                _screenMonitorTimer.Start();


                MessageBox.Show(
                    "Connection error while logging out.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );


                LogoutButton.IsEnabled =
                    true;

                LogoutButton.Content =
                    "Logout";
            }
        }


        // ==========================================
        // NOTIFICATION TIMER
        // ==========================================

        private async void NotificationTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_isLoggingOut)
            {
                return;
            }

            await LoadAnnouncements();
        }


        // ==========================================
        // LOAD ANNOUNCEMENTS
        // ==========================================

        private async Task LoadAnnouncements()
        {
            try
            {
                var announcements =
                    await _httpClient.GetFromJsonAsync<
                        List<StudentAnnouncement>>(
                            "api/Announcement");

                if (announcements == null)
                {
                    return;
                }

                var activeAnnouncements =
                    announcements.FindAll(
                        a => a.IsActive &&
                             (!a.ExpiresAt.HasValue ||
                              a.ExpiresAt.Value > DateTime.Now));

                if (activeAnnouncements.Count == 0)
                {
                    return;
                }

                activeAnnouncements.Sort(
                    (a, b) =>
                        b.AnnouncementId.CompareTo(
                            a.AnnouncementId));

                StudentAnnouncement latest =
                    activeAnnouncements[0];

                if (!_announcementStateInitialized)
                {
                    _lastAnnouncementId =
                        latest.AnnouncementId;

                    _announcementStateInitialized = true;

                    return;
                }

                if (latest.AnnouncementId >
                    _lastAnnouncementId)
                {
                    foreach (
                        StudentAnnouncement announcement
                        in activeAnnouncements)
                    {
                        if (announcement.AnnouncementId <=
                            _lastAnnouncementId)
                        {
                            break;
                        }

                        string message =
                            $"{announcement.Title}: " +
                            announcement.Message;

                        AddStudentNotification(
                            "NEW MIS ANNOUNCEMENT",
                            message);

                        ShowStudentNotification(
                            "NEW MIS ANNOUNCEMENT",
                            message);
                    }

                    _lastAnnouncementId =
                        latest.AnnouncementId;
                }
            }
            catch
            {
                // Keep the student session running if
                // announcements are temporarily unavailable.
            }
        }


        // ==========================================
        // ADD NOTIFICATION
        // ==========================================

        private void AddStudentNotification(
            string title,
            string message)
        {
            _notifications.Insert(
                0,
                new StudentNotification
                {
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.Now
                });

            if (_notifications.Count > 20)
            {
                _notifications.RemoveAt(
                    _notifications.Count - 1);
            }

            _unreadNotificationCount++;

            UpdateNotificationBadge();
        }


        // ==========================================
        // SHOW POPUP
        // ==========================================

        private void ShowStudentNotification(
            string title,
            string message)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        // ==========================================
        // NOTIFICATION BUTTON
        // ==========================================

        private void NotificationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Window dialog =
                new Window
                {
                    Title =
                        "SmartLab - Notifications",

                    Width = 520,
                    Height = 430,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterScreen,

                    ResizeMode =
                        ResizeMode.NoResize,

                    Background =
                        Brushes.White
                };

            Grid root =
                new Grid
                {
                    Margin =
                        new Thickness(20)
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

            TextBlock title =
                new TextBlock
                {
                    Text = "Notifications",

                    FontSize = 24,

                    FontWeight =
                        FontWeights.Bold,

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                24,
                                37,
                                54)),

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            15)
                };

            Grid.SetRow(title, 0);

            root.Children.Add(title);

            ListBox list =
                new ListBox
                {
                    BorderThickness =
                        new Thickness(1)
                };

            if (_notifications.Count == 0)
            {
                list.Items.Add(
                    new TextBlock
                    {
                        Text =
                            "No notifications yet.",

                        Padding =
                            new Thickness(10),

                        Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    101,
                                    117,
                                    138))
                    });
            }
            else
            {
                foreach (
                    StudentNotification notification
                    in _notifications)
                {
                    StackPanel item =
                        new StackPanel
                        {
                            Margin =
                                new Thickness(
                                    10)
                        };

                    item.Children.Add(
                        new TextBlock
                        {
                            Text =
                                notification.Title,

                            FontWeight =
                                FontWeights.Bold,

                            Foreground =
                                new SolidColorBrush(
                                    Color.FromRgb(
                                        24,
                                        37,
                                        54))
                        });

                    item.Children.Add(
                        new TextBlock
                        {
                            Text =
                                notification.Message,

                            TextWrapping =
                                TextWrapping.Wrap,

                            Margin =
                                new Thickness(
                                    0,
                                    5,
                                    0,
                                    3)
                        });

                    item.Children.Add(
                        new TextBlock
                        {
                            Text =
                                notification.CreatedAt
                                    .ToString(
                                        "MM/dd/yyyy HH:mm:ss"),

                            FontSize = 11,

                            Foreground =
                                new SolidColorBrush(
                                    Color.FromRgb(
                                        101,
                                        117,
                                        138))
                        });

                    list.Items.Add(item);
                }
            }

            Grid.SetRow(list, 1);

            root.Children.Add(list);

            StackPanel buttons =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0,
                            15,
                            0,
                            0)
                };

            Button markReadButton =
                new Button
                {
                    Content =
                        "MARK ALL AS READ",

                    Width = 145,

                    Height = 35,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            10,
                            0)
                };

            markReadButton.Click +=
                (s, e) =>
                {
                    _unreadNotificationCount = 0;

                    UpdateNotificationBadge();

                    dialog.Close();
                };

            Button closeButton =
                new Button
                {
                    Content = "CLOSE",

                    Width = 90,

                    Height = 35
                };

            closeButton.Click +=
                (s, e) =>
                {
                    dialog.Close();
                };

            buttons.Children.Add(
                markReadButton);

            buttons.Children.Add(
                closeButton);

            Grid.SetRow(buttons, 2);

            root.Children.Add(buttons);

            dialog.Content = root;

            dialog.ShowDialog();
        }


        // ==========================================
        // UPDATE NOTIFICATION BADGE
        // ==========================================

        private void UpdateNotificationBadge()
        {
            NotificationCountText.Text =
                _unreadNotificationCount.ToString();

            NotificationCountBadge.Visibility =
                _unreadNotificationCount > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }


        // ==========================================
        // WINDOW CLOSED
        // ==========================================

        protected override void OnClosed(
            EventArgs e)
        {
            _isLoggingOut = true;


            // Stop heartbeat.

            _heartbeatTimer.Stop();


            // Stop screen monitoring.

            _screenMonitorTimer.Stop();

            _notificationTimer.Stop();


            // Dispose HTTP client.

            _httpClient.Dispose();


            base.OnClosed(e);
        }
    }


    // ==============================================
    // STUDENT ANNOUNCEMENT
    // ==============================================

    public class StudentAnnouncement
    {
        public int AnnouncementId { get; set; }

        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public bool IsActive { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }


    // ==============================================
    // STUDENT NOTIFICATION
    // ==============================================

    public class StudentNotification
    {
        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }
    }


    // ==============================================
    // SCREEN MONITORING STATUS
    // ==============================================

    public class ScreenMonitoringStatus
    {
        public int PcId { get; set; }

        public bool Monitoring { get; set; }
    }
}