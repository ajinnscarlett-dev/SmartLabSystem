using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;

namespace SmartLab.Client
{
    public partial class TeacherDashboard : Window
    {
        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _refreshTimer;
        private readonly string _username;
        private readonly int _userId;

        // ==========================================
        // NOTIFICATION STATE
        // ==========================================

        private readonly Dictionary<int, string> _assistanceNotificationState =
            new Dictionary<int, string>();

        private readonly HashSet<int> _knownAnnouncementIds =
            new HashSet<int>();

        private bool _notificationStateInitialized;

        private int _unreadNotificationCount = 0;

        private readonly List<SmartLabNotification> _notifications =
            new List<SmartLabNotification>();

        public TeacherDashboard(string username, int userId)
        {
            InitializeComponent();

            _username = username;
            _userId = userId;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7277/")
            };

            TeacherNameText.Text = $"Teacher: {_username}";

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _refreshTimer.Tick += async (sender, e) =>
            {
                await LoadPCs();
                await LoadAnnouncements();
                await LoadAssistanceHistory();
            };

            _refreshTimer.Start();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPCs();
            await LoadAnnouncements();
            await LoadAssistanceHistory();
        }

        private async Task LoadPCs()
        {
            try
            {
                var pcs =
                    await _httpClient.GetFromJsonAsync<List<TeacherPcInfo>>(
                        "api/PC");

                PcGrid.ItemsSource =
                    pcs ?? new List<TeacherPcInfo>();

                int total = pcs?.Count ?? 0;
                int active = 0;

                if (pcs != null)
                {
                    foreach (var pc in pcs)
                    {
                        if (pc.Status.Equals("Available",
                                StringComparison.OrdinalIgnoreCase) ||
                            pc.Status.Equals("Occupied",
                                StringComparison.OrdinalIgnoreCase) ||
                            pc.Status.Equals("In Use",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            active++;
                        }
                    }
                }

                PcStatusSummaryText.Text =
                    $"{active} active / {total} PCs";

                StatusText.Text =
                    $"Connected • Updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Unable to load PC status: {ex.Message}";
            }
        }

        private async Task LoadAnnouncements()
        {
            try
            {
                var announcements =
                    await _httpClient.GetFromJsonAsync<
                        List<TeacherAnnouncement>>(
                            "api/Announcement");

                var currentAnnouncements =
                    announcements ?? new List<TeacherAnnouncement>();

                AnnouncementGrid.ItemsSource =
                    currentAnnouncements;

                // ==========================================
                // DETECT NEW MIS ANNOUNCEMENT
                // ==========================================

                if (_notificationStateInitialized)
                {
                    var newAnnouncement =
                        currentAnnouncements
                            .Where(a => !_knownAnnouncementIds.Contains(a.AnnouncementId))
                            .OrderByDescending(a => a.CreatedAt)
                            .FirstOrDefault();

                    if (newAnnouncement != null)
                    {
                        string message =
                            $"{newAnnouncement.Title}: {newAnnouncement.Message}";

                        AddNotification(
                            "NEW MIS ANNOUNCEMENT",
                            message);

                        ShowNotification(
                            "NEW MIS ANNOUNCEMENT",
                            message);
                    }
                }

                _knownAnnouncementIds.Clear();

                foreach (var announcement in currentAnnouncements)
                {
                    _knownAnnouncementIds.Add(
                        announcement.AnnouncementId);
                }
            }
            catch
            {
                // Keep dashboard usable if announcements
                // are temporarily unavailable.
            }
        }

        private async Task LoadAssistanceHistory()
        {
            try
            {
                var history =
                    await _httpClient.GetFromJsonAsync<
                        List<TeacherServiceDeskTicket>>(
                            $"api/ServiceDesk/teacher/{_userId}");

                var currentHistory =
                    history ?? new List<TeacherServiceDeskTicket>();

                HistoryGrid.ItemsSource =
                    currentHistory;

                // ==========================================
                // DETECT MIS RESPONSE / STATUS CHANGE
                // ==========================================

                if (_notificationStateInitialized)
                {
                    TeacherServiceDeskTicket? changedTicket = null;

                    foreach (var ticket in currentHistory
                        .OrderByDescending(t => t.CreatedAt))
                    {
                        string signature = BuildAssistanceSignature(ticket);

                        if (_assistanceNotificationState.TryGetValue(
                                ticket.ServiceDeskTicketId,
                                out string? previousSignature) &&
                            previousSignature != signature)
                        {
                            changedTicket = ticket;
                            break;
                        }
                    }

                    if (changedTicket != null &&
                        !string.Equals(
                            changedTicket.Status,
                            "Open",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string response =
                            string.IsNullOrWhiteSpace(
                                changedTicket.ResolutionNotes)
                                ? $"Status: {changedTicket.Status}"
                                : changedTicket.ResolutionNotes;

                        string message =
                            $"{changedTicket.Subject}: {response}";

                        AddNotification(
                            "MIS SERVICE DESK UPDATE",
                            message);

                        ShowNotification(
                            "MIS SERVICE DESK UPDATE",
                            message);

                        MessageBox.Show(
                            $"MIS replied to your assistance request.\n\n" +
                            $"Subject: {changedTicket.Subject}\n" +
                            $"Status: {changedTicket.Status}\n\n" +
                            $"{response}",
                            "SmartLab - MIS Update",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                _assistanceNotificationState.Clear();

                foreach (var ticket in currentHistory)
                {
                    _assistanceNotificationState[
                        ticket.ServiceDeskTicketId] =
                        BuildAssistanceSignature(ticket);
                }

                _notificationStateInitialized = true;
            }
            catch
            {
                // Keep dashboard usable if Service Desk
                // is temporarily unavailable.
            }
        }


        private static string BuildAssistanceSignature(
            TeacherServiceDeskTicket ticket)
        {
            return string.Join(
                "|",
                ticket.Status ?? string.Empty,
                ticket.ResolutionNotes ?? string.Empty,
                ticket.AssignedToUsername ?? string.Empty,
                ticket.ResolvedAt?.ToString("O") ?? string.Empty);
        }


        // ==========================================
        // SHOW NOTIFICATION BANNER
        // ==========================================

        // ==========================================
        // NOTIFICATION BADGE / INBOX
        // ==========================================

        private void AddNotification(
            string title,
            string message)
        {
            _notifications.Insert(
                0,
                new SmartLabNotification
                {
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.Now
                });

            // Keep the notification inbox small.
            if (_notifications.Count > 20)
            {
                _notifications.RemoveAt(
                    _notifications.Count - 1);
            }

            _unreadNotificationCount++;

            UpdateNotificationBadge();
        }


        private void UpdateNotificationBadge()
        {
            NotificationCountText.Text =
                _unreadNotificationCount.ToString();

            NotificationCountBadge.Visibility =
                _unreadNotificationCount > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }


        private void ShowNotification(
            string title,
            string message)
        {
            NotificationTitleText.Text = title;
            NotificationMessageText.Text = message;
            NotificationBanner.Visibility =
                Visibility.Visible;
        }


        private void DismissNotificationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NotificationBanner.Visibility =
                Visibility.Collapsed;
        }


        private void NotificationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Window dialog = new Window
            {
                Title = "SmartLab - Notifications",
                Width = 560,
                Height = 500,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = Brushes.White
            };

            Grid root = new Grid
            {
                Margin = new Thickness(20)
            };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            TextBlock title = new TextBlock
            {
                Text = "Notifications",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(24, 37, 54)),
                Margin = new Thickness(0, 0, 0, 15)
            };

            Grid.SetRow(title, 0);
            root.Children.Add(title);

            ListBox list = new ListBox
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };

            if (_notifications.Count == 0)
            {
                list.Items.Add(
                    new TextBlock
                    {
                        Text = "No notifications yet.",
                        Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(101, 117, 138)),
                        Padding = new Thickness(10)
                    });
            }
            else
            {
                foreach (SmartLabNotification notification
                    in _notifications)
                {
                    StackPanel item =
                        new StackPanel
                        {
                            Margin =
                                new Thickness(
                                    8,
                                    8,
                                    8,
                                    10)
                        };

                    item.Children.Add(
                        new TextBlock
                        {
                            Text =
                                notification.Title,
                            FontWeight =
                                FontWeights.Bold,
                            FontSize = 14,
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
                                    4,
                                    0,
                                    3),
                            Foreground =
                                new SolidColorBrush(
                                    Color.FromRgb(
                                        75,
                                        93,
                                        112))
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

            Button clearButton =
                new Button
                {
                    Content = "MARK ALL AS READ",
                    Width = 145,
                    Height = 35,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            10,
                            0)
                };

            clearButton.Click +=
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

            buttons.Children.Add(clearButton);
            buttons.Children.Add(closeButton);

            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            dialog.Content = root;
            dialog.ShowDialog();
        }

        private void ViewPcScreenButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (PcGrid.SelectedItem is not TeacherPcInfo pc)
            {
                MessageBox.Show(
                    "Select a student PC first.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (pc.PcId <= 0)
            {
                MessageBox.Show(
                    "The selected PC does not have a valid PC ID.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            LiveScreenWindow window =
                new LiveScreenWindow(
                    pc.PcId,
                    pc.PcNumber);

            window.Owner = this;
            window.ShowDialog();
        }

        private async void NeedAssistanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new NeedAssistanceWindow(
                    _username,
                    _userId,
                    _httpClient);

            dialog.Owner = this;

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                await LoadAssistanceHistory();
            }
        }

        private async void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadPCs();
            await LoadAnnouncements();
            await LoadAssistanceHistory();
        }

        private void LogoutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _refreshTimer.Stop();

            MainWindow loginWindow =
                new MainWindow();

            loginWindow.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            _httpClient.Dispose();
            base.OnClosed(e);
        }
    }

    public class TeacherPcInfo
    {
        public int PcId { get; set; }
        public string PcNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Username { get; set; }
        public DateTime? LastSeen { get; set; }
    }

    public class TeacherAnnouncement
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class SmartLabNotification
    {
        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }
    }


    public class TeacherServiceDeskTicket
    {
        public int ServiceDeskTicketId { get; set; }
        public int TeacherUserId { get; set; }
        public string TeacherUsername { get; set; } = string.Empty;
        public string? PCNumber { get; set; }
        public string? Location { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    public class NeedAssistanceWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _teacherUsername;
        private readonly int _teacherUserId;

        private readonly ComboBox _categoryBox;
        private readonly TextBox _subjectBox;
        private readonly TextBox _descriptionBox;
        private readonly TextBox _pcBox;
        private readonly TextBox _locationBox;

        public NeedAssistanceWindow(
            string teacherUsername,
            int teacherUserId,
            HttpClient httpClient)
        {
            _teacherUsername = teacherUsername;
            _teacherUserId = teacherUserId;
            _httpClient = httpClient;

            Title = "SmartLab - Need Assistance";
            Width = 520;
            Height = 680;
            MinHeight = 680;
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background =
                System.Windows.Media.Brushes.White;

            var root = new StackPanel
            {
                Margin = new Thickness(25)
            };

            root.Children.Add(new TextBlock
            {
                Text = "Need Assistance",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground =
                    System.Windows.Media.Brushes.DarkSlateGray,
                Margin = new Thickness(0, 0, 0, 20)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Request help from MIS / IT Staff.",
                Foreground =
                    System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 20)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Category",
                FontWeight = FontWeights.Bold
            });

            _categoryBox = new ComboBox
            {
                Height = 35,
                Margin = new Thickness(0, 5, 0, 15)
            };

            _categoryBox.Items.Add("Hardware");
            _categoryBox.Items.Add("Software");
            _categoryBox.Items.Add("Network");
            _categoryBox.Items.Add("Account");
            _categoryBox.Items.Add("Classroom Equipment");
            _categoryBox.Items.Add("Other");
            _categoryBox.SelectedIndex = 0;

            root.Children.Add(_categoryBox);

            root.Children.Add(new TextBlock
            {
                Text = "Subject",
                FontWeight = FontWeights.Bold
            });

            _subjectBox = new TextBox
            {
                Height = 35,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(8)
            };

            root.Children.Add(_subjectBox);

            root.Children.Add(new TextBlock
            {
                Text = "PC Number (optional)",
                FontWeight = FontWeights.Bold
            });

            _pcBox = new TextBox
            {
                Height = 35,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(8)
            };

            root.Children.Add(_pcBox);

            root.Children.Add(new TextBlock
            {
                Text = "Location (optional)",
                FontWeight = FontWeights.Bold
            });

            _locationBox = new TextBox
            {
                Height = 35,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(8)
            };

            root.Children.Add(_locationBox);

            root.Children.Add(new TextBlock
            {
                Text = "Description",
                FontWeight = FontWeights.Bold
            });

            _descriptionBox = new TextBox
            {
                Height = 80,
                Margin = new Thickness(0, 5, 0, 12),
                Padding = new Thickness(8),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };

            root.Children.Add(_descriptionBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "CANCEL",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0)
            };

            cancelButton.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttons.Children.Add(cancelButton);

            var submitButton = new Button
            {
                Content = "SUBMIT REQUEST",
                Width = 145,
                Height = 35,
                FontWeight = FontWeights.Bold
            };

            submitButton.Click += async (s, e) =>
            {
                await SubmitRequest(submitButton);
            };

            buttons.Children.Add(submitButton);
            root.Children.Add(buttons);

            Content = root;
        }

        private async Task SubmitRequest(Button submitButton)
        {
            string subject = _subjectBox.Text.Trim();
            string description = _descriptionBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(subject))
            {
                MessageBox.Show(
                    "Please enter a subject.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _subjectBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(
                    "Please describe the assistance you need.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _descriptionBox.Focus();
                return;
            }

            submitButton.IsEnabled = false;
            submitButton.Content = "SUBMITTING...";

            try
            {
                var request =
                    new ServiceDeskCreateRequest
                    {
                        TeacherUserId = _teacherUserId,
                        TeacherUsername = _teacherUsername,
                        Category =
                            _categoryBox.SelectedItem?.ToString()
                            ?? "Other",
                        Subject = subject,
                        Description = description,
                        PCNumber =
                            string.IsNullOrWhiteSpace(_pcBox.Text)
                                ? null
                                : _pcBox.Text.Trim(),
                        Location =
                            string.IsNullOrWhiteSpace(_locationBox.Text)
                                ? null
                                : _locationBox.Text.Trim()
                    };

                var response =
                    await _httpClient.PostAsJsonAsync(
                        "api/ServiceDesk",
                        request);

                string responseText =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                MessageBox.Show(
                    "Your assistance request has been submitted.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to submit assistance request." +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                submitButton.IsEnabled = true;
                submitButton.Content = "SUBMIT REQUEST";
            }
        }
    }

    public class ServiceDeskCreateRequest
    {
        public int TeacherUserId { get; set; }

        public string TeacherUsername { get; set; } =
            string.Empty;

        public string? PCNumber { get; set; }

        public string? Location { get; set; }

        public string Category { get; set; } =
            string.Empty;

        public string Subject { get; set; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;
    }
}
