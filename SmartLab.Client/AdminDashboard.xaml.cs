using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class AdminDashboard : Window
    {
        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _refreshTimer;

        private PCInfo? _selectedPc;

        private bool _isDarkTheme = true;

        public AdminDashboard(string username)
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7277/")
            };

            // ==========================================
            // AUTHENTICATED HTTP SESSION
            // ==========================================

            AuthSession.Apply(_httpClient);

            AdminNameText.Text = username;

            // ==========================================
            // AUTOMATIC REFRESH
            // ==========================================

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _refreshTimer.Tick += async (sender, e) =>
            {
                await LoadPCs();
                await LoadActivityLogs();
                await LoadUsers();
                await LoadAnnouncements();
                await LoadServiceDeskTickets();
            };

            _refreshTimer.Start();
        }


        // ==========================================
        // WINDOW LOADED
        // ==========================================

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            HideSelectedPcSidebar();
            await LoadPCs();
            await LoadActivityLogs();
            await LoadUsers();
            await LoadAnnouncements();
        }


        // ==========================================
        // LOAD PCs
        // ==========================================

        private async Task LoadPCs()
        {
            try
            {
                var pcs = await _httpClient
                    .GetFromJsonAsync<List<PCInfo>>(
                        "api/PC");

                if (pcs == null)
                {
                    ApiStatusText.Text =
                        "No PC information found.";

                    return;
                }


                // ==========================================
                // COUNT STATUS
                // ==========================================

                int total = pcs.Count;
                int occupied = 0;
                int available = 0;
                int maintenance = 0;
                int offline = 0;

                foreach (var pc in pcs)
                {
                    if (pc.Status.Equals(
                        "Available",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        available++;
                    }
                    else if (
                        pc.Status.Equals(
                            "Occupied",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        pc.Status.Equals(
                            "In Use",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        occupied++;

                        if (!pc.LastSeen.HasValue ||
                            (DateTime.Now - pc.LastSeen.Value).TotalSeconds > 10)
                        {
                            offline++;
                        }
                    }
                    else if (
                        pc.Status.Equals(
                            "Maintenance",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        maintenance++;
                    }
                    else
                    {
                        offline++;
                    }
                }


                // ==========================================
                // UPDATE SUMMARY
                // ==========================================

                TotalPcText.Text =
                    total.ToString();

                OccupiedPcText.Text =
                    occupied.ToString();

                AvailablePcText.Text =
                    available.ToString();

                MaintenancePcText.Text =
                    maintenance.ToString();

                OfflinePcText.Text =
                    offline.ToString();
                // ==========================================
                // UPDATE PC CARDS
                // ==========================================

                PcGrid.Children.Clear();

                foreach (var pc in pcs)
                {
                    PcGrid.Children.Add(
                        CreatePcCard(pc));
                }


                ApiStatusText.Text =
                    $"Connected • Updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"Unable to connect to SmartLab Server: {ex.Message}";
            }
        }


        // ==========================================
        // LOAD ACTIVITY LOGS
        // ==========================================

        private async Task LoadActivityLogs()
        {
            try
            {
                var logs =
                    await _httpClient
                        .GetFromJsonAsync<List<ActivityLogInfo>>(
                            "api/ActivityLog");

                if (logs == null)
                {
                    ActivityLogGrid.ItemsSource =
                        new List<ActivityLogInfo>();
                    return;
                }

                ActivityLogGrid.ItemsSource = logs;
            }
            catch (Exception ex)
            {
                ActivityLogGrid.ItemsSource =
                    new List<ActivityLogInfo>();

                ApiStatusText.Text =
                    $"Activity log error: {ex.Message}";
            }
        }


        // ==========================================
        // LOAD USERS
        // ==========================================

        private async Task LoadUsers()
        {
            try
            {
                var users =
                    await _httpClient
                        .GetFromJsonAsync<List<UserInfo>>(
                            "api/User");

                UserGrid.ItemsSource =
                    users ?? new List<UserInfo>();
            }
            catch (Exception ex)
            {
                UserGrid.ItemsSource =
                    new List<UserInfo>();

                ApiStatusText.Text =
                    $"User management error: {ex.Message}";
            }
        }


        // ==========================================
        // REFRESH USERS
        // ==========================================

        private async void RefreshUsersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadUsers();
        }


        // ==========================================
        // ADD USER
        // ==========================================

        private void AddUserButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowUserDialog(null);
        }


        // ==========================================
        // EDIT USER ROLE
        // ==========================================

        private async void EditUserRoleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not UserInfo user)
            {
                return;
            }

            Window roleDialog =
                new Window
                {
                    Title = $"Change Role - {user.Username}",
                    Width = 400,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = this,
                    Background = Brushes.White
                };

            StackPanel rolePanel =
                new StackPanel
                {
                    Margin = new Thickness(22)
                };

            rolePanel.Children.Add(
                new TextBlock
                {
                    Text = $"Change role for {user.Username}",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

            ComboBox roleCombo =
                new ComboBox
                {
                    Height = 36
                };

            roleCombo.Items.Add("Student");
            roleCombo.Items.Add("Teacher");
            roleCombo.Items.Add("Admin");

            roleCombo.SelectedItem =
                roleCombo.Items.Contains(user.Role)
                    ? user.Role
                    : "Student";

            rolePanel.Children.Add(roleCombo);

            StackPanel roleButtons =
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 18, 0, 0)
                };

            Button cancelRoleButton =
                new Button
                {
                    Content = "CANCEL",
                    Width = 90,
                    Height = 32,
                    Margin = new Thickness(0, 0, 8, 0)
                };

            Button saveRoleButton =
                new Button
                {
                    Content = "SAVE",
                    Width = 90,
                    Height = 32
                };

            cancelRoleButton.Click +=
                (s, e) => roleDialog.Close();

            saveRoleButton.Click += async (s, e) =>
            {
                string newRole =
                    roleCombo.SelectedItem?.ToString()
                    ?? "Student";

                try
                {
                    saveRoleButton.IsEnabled = false;
                    cancelRoleButton.IsEnabled = false;

                    var request =
                        new UserRoleUpdateRequest
                        {
                            Role = newRole,
                            PerformedBy = AdminNameText.Text
                        };

                    var response =
                        await _httpClient.PutAsJsonAsync(
                            $"api/User/{user.UserId}/role",
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

                        saveRoleButton.IsEnabled = true;
                        cancelRoleButton.IsEnabled = true;
                        return;
                    }

                    roleDialog.Close();
                    await LoadUsers();
                    await LoadActivityLogs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to change user role.\n\n" + ex.Message,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    saveRoleButton.IsEnabled = true;
                    cancelRoleButton.IsEnabled = true;
                }
            };

            roleButtons.Children.Add(cancelRoleButton);
            roleButtons.Children.Add(saveRoleButton);

            rolePanel.Children.Add(roleButtons);

            roleDialog.Content = rolePanel;
            roleDialog.ShowDialog();

            return;
        }

        // ==========================================
        // RESET USER PASSWORD
        // ==========================================

        private async void ResetUserPasswordButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not UserInfo user)
            {
                return;
            }

            ShowPasswordResetDialog(user);
        }


        // ==========================================
        // ADD USER DIALOG
        // ==========================================

        private void ShowUserDialog(UserInfo? existingUser)
        {
            Window dialog = new Window
            {
                Title = "Add User",
                Width = 460,
                Height = 430,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = Brushes.White
            };

            StackPanel panel = new StackPanel
            {
                Margin = new Thickness(25)
            };

            TextBlock title = new TextBlock
            {
                Text = "Add New User",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(24, 37, 54)),
                Margin = new Thickness(0, 0, 0, 20)
            };

            TextBox usernameBox = new TextBox
            {
                Height = 38,
                Padding = new Thickness(8)
            };

            PasswordBox passwordBox = new PasswordBox
            {
                Height = 38,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 0)
            };

            ComboBox roleBox = new ComboBox
            {
                Height = 38,
                Margin = new Thickness(0, 5, 0, 0)
            };
            roleBox.Items.Add("Student");
            roleBox.Items.Add("Teacher");
            roleBox.Items.Add("Admin");
            roleBox.SelectedIndex = 0;

            panel.Children.Add(title);
            panel.Children.Add(new TextBlock { Text = "Username", FontWeight = FontWeights.Bold });
            panel.Children.Add(usernameBox);
            panel.Children.Add(new TextBlock { Text = "Password", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 0) });
            panel.Children.Add(passwordBox);
            panel.Children.Add(new TextBlock { Text = "Role", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 0) });
            panel.Children.Add(roleBox);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 25, 0, 0)
            };

            Button cancelButton = new Button
            {
                Content = "CANCEL",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Button addButton = new Button
            {
                Content = "ADD",
                Width = 100,
                Height = 35
            };

            cancelButton.Click += (s, e) => dialog.Close();

            addButton.Click += async (s, e) =>
            {
                string username = usernameBox.Text.Trim();
                string password = passwordBox.Password;
                string role = roleBox.SelectedItem?.ToString() ?? "Student";

                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Username is required.", "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
                    usernameBox.Focus();
                    return;
                }

                if (password.Length < 6)
                {
                    MessageBox.Show("Password must be at least 6 characters.", "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
                    passwordBox.Focus();
                    return;
                }

                addButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                addButton.Content = "ADDING...";

                try
                {
                    var request = new UserCreateRequest
                    {
                        Username = username,
                        Password = password,
                        Role = role,
                        PerformedBy = AdminNameText.Text
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/User", request);
                    string responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(responseText, "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
                        addButton.IsEnabled = true;
                        cancelButton.IsEnabled = true;
                        addButton.Content = "ADD";
                        return;
                    }

                    dialog.Close();
                    await LoadUsers();
                    await LoadActivityLogs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to add user.\n\n" + ex.Message, "SmartLab", MessageBoxButton.OK, MessageBoxImage.Error);
                    addButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                    addButton.Content = "ADD";
                }
            };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(addButton);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
        }


        // ==========================================
        // RESET PASSWORD DIALOG
        // ==========================================

        private void ShowPasswordResetDialog(UserInfo user)
        {
            Window dialog = new Window
            {
                Title = $"Reset Password - {user.Username}",
                Width = 430,
                Height = 270,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = Brushes.White
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(25) };
            panel.Children.Add(new TextBlock
            {
                Text = "Reset Password",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"User: {user.Username}",
                Margin = new Thickness(0, 0, 0, 10)
            });

            PasswordBox passwordBox = new PasswordBox
            { Height = 38, Padding = new Thickness(8) };
            panel.Children.Add(new TextBlock { Text = "New Password", FontWeight = FontWeights.Bold });
            panel.Children.Add(passwordBox);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            Button cancelButton = new Button { Content = "CANCEL", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            Button resetButton = new Button { Content = "RESET", Width = 100, Height = 35 };
            cancelButton.Click += (s, e) => dialog.Close();

            resetButton.Click += async (s, e) =>
            {
                if (passwordBox.Password.Length < 6)
                {
                    MessageBox.Show("Password must be at least 6 characters.", "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                resetButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                resetButton.Content = "RESETTING...";

                try
                {
                    var request = new UserPasswordResetRequest
                    {
                        NewPassword = passwordBox.Password,
                        PerformedBy = AdminNameText.Text
                    };

                    var response = await _httpClient.PutAsJsonAsync(
                        $"api/User/{user.UserId}/password", request);
                    string responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(responseText, "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
                        resetButton.IsEnabled = true;
                        cancelButton.IsEnabled = true;
                        resetButton.Content = "RESET";
                        return;
                    }

                    dialog.Close();
                    await LoadUsers();
                    await LoadActivityLogs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to reset password.\n\n" + ex.Message, "SmartLab", MessageBoxButton.OK, MessageBoxImage.Error);
                    resetButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                    resetButton.Content = "RESET";
                }
            };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(resetButton);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
        }


        // ==========================================
        // CREATE PC CARD
        // ==========================================

        private Border CreatePcCard(PCInfo pc)
        {
            Color screenColor;

            if (pc.Status.Equals(
                "Available",
                StringComparison.OrdinalIgnoreCase))
            {
                screenColor = Color.FromRgb(31, 177, 69);
            }
            else if (
                pc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                pc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase))
            {
                screenColor = Color.FromRgb(36, 108, 223);
            }
            else if (
                pc.Status.Equals(
                    "Maintenance",
                    StringComparison.OrdinalIgnoreCase))
            {
                screenColor = Color.FromRgb(236, 128, 16);
            }
            else
            {
                screenColor = Color.FromRgb(75, 84, 93);
            }

            Border card =
                new Border
                {
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(9, 19, 28)),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(27, 48, 65)),

                    BorderThickness =
                        new Thickness(1),

                    CornerRadius =
                        new CornerRadius(8),

                    Padding =
                        new Thickness(7),

                    Margin =
                        new Thickness(4),

                    Cursor =
                        Cursors.Hand,

                    Tag = pc
                };

            card.MouseLeftButtonUp +=
                PcCard_Click;

            StackPanel content =
                new StackPanel
                {
                    HorizontalAlignment =
                        HorizontalAlignment.Center
                };

            // Monitor
            Grid monitor =
                new Grid
                {
                    Width = 74,
                    Height = 58,
                    HorizontalAlignment =
                        HorizontalAlignment.Center
                };

            Border monitorFrame =
                new Border
                {
                    Width = 74,
                    Height = 47,
                    CornerRadius =
                        new CornerRadius(4),
                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(64, 74, 84)),
                    BorderThickness =
                        new Thickness(2),
                    Background =
                        new SolidColorBrush(screenColor),
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Top
                };

            Border screen =
                new Border
                {
                    Margin =
                        new Thickness(5),
                    Background =
                        new SolidColorBrush(
                            Color.FromArgb(
                                70,
                                255,
                                255,
                                255)),
                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromArgb(
                                65,
                                255,
                                255,
                                255)),
                    BorderThickness =
                        new Thickness(1)
                };

            monitorFrame.Child = screen;

            Border stand =
                new Border
                {
                    Width = 10,
                    Height = 8,
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(92, 102, 112)),
                    VerticalAlignment =
                        VerticalAlignment.Bottom,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            2)
                };

            Border baseStand =
                new Border
                {
                    Width = 30,
                    Height = 4,
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(74, 84, 94)),
                    VerticalAlignment =
                        VerticalAlignment.Bottom,
                    HorizontalAlignment =
                        HorizontalAlignment.Center
                };

            monitor.Children.Add(monitorFrame);
            monitor.Children.Add(stand);
            monitor.Children.Add(baseStand);

            // Status badge
            string badgeText =
                pc.Status.Equals(
                    "Maintenance",
                    StringComparison.OrdinalIgnoreCase)
                    ? "!"
                    :
                    pc.Status.Equals(
                        "Occupied",
                        StringComparison.OrdinalIgnoreCase)
                    || pc.Status.Equals(
                        "In Use",
                        StringComparison.OrdinalIgnoreCase)
                    ? "•"
                    :
                    pc.Status.Equals(
                        "Available",
                        StringComparison.OrdinalIgnoreCase)
                    ? "✓"
                    : "◷";

            Border badge =
                new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius =
                        new CornerRadius(9),
                    Background =
                        new SolidColorBrush(
                            screenColor),
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    VerticalAlignment =
                        VerticalAlignment.Top,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            1,
                            0)
                };

            badge.Child =
                new TextBlock
                {
                    Text = badgeText,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            Grid monitorLayer =
                new Grid
                {
                    Width = 78,
                    Height = 60
                };

            monitorLayer.Children.Add(monitor);
            monitorLayer.Children.Add(badge);

            TextBlock pcNumber =
                new TextBlock
                {
                    Text = pc.PcNumber,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    Margin =
                        new Thickness(0, 2, 0, 0)
                };

            TextBlock dot =
                new TextBlock
                {
                    Text = "●",
                    FontSize = 12,
                    Foreground =
                        new SolidColorBrush(screenColor),
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    Margin =
                        new Thickness(0, 1, 0, 0)
                };

            content.Children.Add(monitorLayer);
            content.Children.Add(pcNumber);
            content.Children.Add(dot);

            card.Child = content;

            return card;
        }


        // ==========================================
        // VIEW SCREEN
        // ==========================================

        private void ViewScreenButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }


            if (button.Tag is not PCInfo pc)
            {
                return;
            }


            // ==========================================
            // CHECK PC STATUS
            // ==========================================

            bool isOccupied =
                pc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                pc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);


            if (!isOccupied)
            {
                MessageBox.Show(
                    "This PC is not currently occupied.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }


            // ==========================================
            // CHECK HEARTBEAT
            // ==========================================

            bool isOnline = false;


            if (pc.LastSeen.HasValue)
            {
                TimeSpan age =
                    DateTime.Now -
                    pc.LastSeen.Value;

                isOnline =
                    age.TotalSeconds <= 10;
            }


            if (!isOnline)
            {
                MessageBox.Show(
                    "This PC is not currently online.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }


            // ==========================================
            // OPEN LIVE SCREEN WINDOW
            // ==========================================

            LiveScreenWindow screenWindow =
                new LiveScreenWindow(
                    pc.PcId,
                    pc.PcNumber
                );


            screenWindow.Owner = this;

            screenWindow.Show();
        }

        // ==========================================
        // PC CARD CLICK
        // ==========================================

        private void PcCard_Click(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not Border border)
            {
                return;
            }


            // If the click came from a button,
            // do not open the details window.

            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject? current =
                    source;

                while (current != null)
                {
                    if (current is Button)
                    {
                        return;
                    }

                    current =
                        VisualTreeHelper.GetParent(
                            current);
                }
            }


            if (border.Tag is not PCInfo pc)
            {
                return;
            }

            UpdateSelectedPcPanel(pc);

            UpdateSelectedPcPanel(pc);
        }


        private void UpdateSelectedPcPanel(PCInfo pc)
        {
            ShowSelectedPcSidebar();
            _selectedPc = pc;

            if (SelectedPcTitleText != null)
                SelectedPcTitleText.Text = pc.PcNumber;

            if (SelectedPcHintText != null)
                SelectedPcHintText.Text =
                    string.IsNullOrWhiteSpace(pc.Username)
                        ? "No current user"
                        : pc.Username;

            if (SelectedPcCurrentUserText != null)
                SelectedPcCurrentUserText.Text =
                    string.IsNullOrWhiteSpace(pc.Username)
                        ? "None"
                        : pc.Username;

            if (SelectedPcLoginTimeText != null)
                SelectedPcLoginTimeText.Text = "—";

            if (SelectedPcIpText != null)
                SelectedPcIpText.Text = "—";

            if (SelectedPcLabText != null)
                SelectedPcLabText.Text = "ComLab 601";

            if (SelectedPcStatusText != null)
            {
                bool occupied =
                    pc.Status.Equals(
                        "Occupied",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    pc.Status.Equals(
                        "In Use",
                        StringComparison.OrdinalIgnoreCase);

                if (pc.Status.Equals(
                    "Maintenance",
                    StringComparison.OrdinalIgnoreCase))
                {
                    SelectedPcStatusText.Text =
                        "● Maintenance";
                    SelectedPcStatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(240, 139, 26));
                }
                else if (occupied)
                {
                    SelectedPcStatusText.Text =
                        "● In Use";
                    SelectedPcStatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(36, 119, 255));
                }
                else
                {
                    SelectedPcStatusText.Text =
                        "● Available";
                    SelectedPcStatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(41, 199, 111));
                }
            }

            if (SelectedPcLastSeenText != null)
            {
                SelectedPcLastSeenText.Text =
                    pc.LastSeen.HasValue
                        ? pc.LastSeen.Value.ToString(
                            "MM/dd/yyyy HH:mm:ss")
                        : "—";
            }
        }



        // ==========================================
        // DASHBOARD / NAVIGATION BUTTONS
        // ==========================================

        private void SelectAdminModule(int index)
        {
            if (AdminModulesTab == null)
            {
                return;
            }

            AdminModulesTab.SelectedIndex = index;

            // Scroll the main view so the tab module is visible.
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    AdminModulesTab.BringIntoView();
                }));
        }


        private void DashboardNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainScrollViewer.ScrollToHome();
        }


        private void UserManagementNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectAdminModule(1);
        }


        private void LaboratoriesNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Laboratory selection is available from the laboratory selector.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void PcManagementNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectAdminModule(0);
        }


        private void ReportsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectAdminModule(4);
        }


        private void ServiceDeskNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectAdminModule(3);
        }


        private void AnnouncementsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectAdminModule(2);
        }


        private void SettingsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Settings is reserved for the next admin module.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void HeaderThemeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;

            SetThemeBrush("Bg",
                _isDarkTheme ? "#06111A" : "#F4F7FA");

            SetThemeBrush("HeaderBg",
                _isDarkTheme ? "#07121C" : "#FFFFFF");

            SetThemeBrush("Panel",
                _isDarkTheme ? "#0A1620" : "#FFFFFF");

            SetThemeBrush("Panel2",
                _isDarkTheme ? "#0D1B26" : "#EEF3F7");

            SetThemeBrush("Border",
                _isDarkTheme ? "#1A3042" : "#CBD8E2");

            SetThemeBrush("Text",
                _isDarkTheme ? "#F4F8FB" : "#172534");

            SetThemeBrush("Muted",
                _isDarkTheme ? "#88A0B3" : "#5B6E80");

            SetThemeBrush("ButtonBg",
                _isDarkTheme ? "#102334" : "#FFFFFF");

            SetThemeBrush("ButtonBorder",
                _isDarkTheme ? "#22415A" : "#C6D5E0");

            SetThemeBrush("InputBg",
                _isDarkTheme ? "#0A1722" : "#FFFFFF");

            SetThemeBrush("GridBg",
                _isDarkTheme ? "#08131D" : "#FFFFFF");

            SetThemeBrush("GridAlt",
                _isDarkTheme ? "#0E1C27" : "#F6F9FB");

            if (ThemeToggleButton != null)
            {
                ThemeToggleButton.Content =
                    _isDarkTheme ? "☾" : "☀";

                ThemeToggleButton.ToolTip =
                    _isDarkTheme
                        ? "Switch to light mode"
                        : "Switch to dark mode";
            }
        }


        private void SetThemeBrush(
            string key,
            string hex)
        {
            if (Resources[key] is SolidColorBrush brush)
            {
                brush.Color =
                    (Color)ColorConverter.ConvertFromString(hex);
            }
        }


        private void HeaderBellButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var items =
                new List<AnnouncementInfo>();

            if (AnnouncementGrid.ItemsSource
                is IEnumerable<AnnouncementInfo> announcements)
            {
                foreach (AnnouncementInfo announcement in announcements)
                {
                    if (!announcement.IsActive)
                    {
                        continue;
                    }

                    if (announcement.ExpiresAt.HasValue &&
                        announcement.ExpiresAt.Value <= DateTime.Now)
                    {
                        continue;
                    }

                    items.Add(announcement);
                }
            }

            Window dialog =
                new Window
                {
                    Title = "SmartLab - Notifications",
                    Width = 560,
                    Height = 500,
                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,
                    ResizeMode =
                        ResizeMode.NoResize,
                    Owner = this,
                    Background =
                        _isDarkTheme
                            ? new SolidColorBrush(
                                Color.FromRgb(7, 16, 25))
                            : new SolidColorBrush(
                                Color.FromRgb(244, 247, 250))
                };

            Grid root =
                new Grid
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
                    Height =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            TextBlock title =
                new TextBlock
                {
                    Text = "Notifications",
                    FontSize = 24,
                    FontWeight =
                        FontWeights.Bold,
                    Foreground =
                        _isDarkTheme
                            ? Brushes.White
                            : new SolidColorBrush(
                                Color.FromRgb(
                                    23, 37, 52)),
                    Margin =
                        new Thickness(0, 0, 0, 15)
                };

            Grid.SetRow(title, 0);
            root.Children.Add(title);

            ListBox list =
                new ListBox
                {
                    Background =
                        _isDarkTheme
                            ? new SolidColorBrush(
                                Color.FromRgb(
                                    9, 19, 28))
                            : Brushes.White,
                    Foreground =
                        _isDarkTheme
                            ? Brushes.White
                            : new SolidColorBrush(
                                Color.FromRgb(
                                    23, 37, 52)),
                    BorderBrush =
                        _isDarkTheme
                            ? new SolidColorBrush(
                                Color.FromRgb(
                                    30, 52, 70))
                            : new SolidColorBrush(
                                Color.FromRgb(
                                    203, 216, 226))
                };

            if (items.Count == 0)
            {
                list.Items.Add(
                    new TextBlock
                    {
                        Text =
                            "No active notifications.",
                        Padding =
                            new Thickness(10),
                        Foreground =
                            _isDarkTheme
                                ? new SolidColorBrush(
                                    Color.FromRgb(
                                        136, 160, 179))
                                : new SolidColorBrush(
                                    Color.FromRgb(
                                        91, 110, 128))
                    });
            }
            else
            {
                foreach (AnnouncementInfo item in items)
                {
                    StackPanel card =
                        new StackPanel
                        {
                            Margin =
                                new Thickness(
                                    10,
                                    8,
                                    10,
                                    8)
                        };

                    card.Children.Add(
                        new TextBlock
                        {
                            Text = item.Title,
                            FontWeight =
                                FontWeights.Bold,
                            FontSize = 14,
                            Foreground =
                                _isDarkTheme
                                    ? Brushes.White
                                    : new SolidColorBrush(
                                        Color.FromRgb(
                                            23, 37, 52))
                        });

                    card.Children.Add(
                        new TextBlock
                        {
                            Text = item.Message,
                            TextWrapping =
                                TextWrapping.Wrap,
                            Margin =
                                new Thickness(
                                    0, 4, 0, 4),
                            Foreground =
                                _isDarkTheme
                                    ? new SolidColorBrush(
                                        Color.FromRgb(
                                            185, 204, 217))
                                    : new SolidColorBrush(
                                        Color.FromRgb(
                                            75, 93, 112))
                        });

                    card.Children.Add(
                        new TextBlock
                        {
                            Text =
                                item.CreatedAt.ToString(
                                    "MM/dd/yyyy HH:mm"),
                            FontSize = 10,
                            Foreground =
                                _isDarkTheme
                                    ? new SolidColorBrush(
                                        Color.FromRgb(
                                            127, 149, 168))
                                    : new SolidColorBrush(
                                        Color.FromRgb(
                                            91, 110, 128))
                        });

                    list.Items.Add(card);
                }
            }

            Grid.SetRow(list, 1);
            root.Children.Add(list);

            Button close =
                new Button
                {
                    Content = "CLOSE",
                    Width = 90,
                    Height = 34,
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    Margin =
                        new Thickness(
                            0, 12, 0, 0),
                    Style =
                        (Style)FindResource(
                            "ActionButton")
                };

            close.Click +=
                (s, e) => dialog.Close();

            Grid.SetRow(close, 2);
            root.Children.Add(close);

            dialog.Content = root;
            dialog.ShowDialog();

            // Opening notifications marks the badge as read.
            if (NotificationBadge != null)
                NotificationBadge.Visibility =
                    Visibility.Collapsed;

            if (NotificationCountText != null)
                NotificationCountText.Text = "0";
        }


        private void ProfileButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                $"Signed in as {AdminNameText.Text}.",
                "SmartLab Administrator",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void GridViewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Grid View is already active.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ListViewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "List View will be added in a later module.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ClearSelectedPcButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideSelectedPcSidebar();
        }


        private void UnsupportedQuickActionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string action =
                sender is Button button &&
                button.Content is string text
                    ? text
                    : "This action";

            if (_selectedPc == null)
            {
                MessageBox.Show(
                    $"Select a PC first to use {action}.",
                    "SmartLab Quick Action",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBox.Show(
                $"Selected PC: {_selectedPc.PcNumber}\n" +
                $"Action: {action}\n\n" +
                "The button is connected to the Admin UI. " +
                "The matching machine-command endpoint is still a backend task.",
                "SmartLab Quick Action",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }



        private void ViewFullScreenButton_Click(
            object sender,
            RoutedEventArgs e)
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

            bool isOccupied =
                _selectedPc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                _selectedPc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);

            bool isOnline =
                _selectedPc.LastSeen.HasValue &&
                (DateTime.Now - _selectedPc.LastSeen.Value)
                    .TotalSeconds <= 10;

            if (!isOccupied)
            {
                MessageBox.Show(
                    "This PC is not currently occupied.\n\n" +
                    "A student must be logged in before Live Screen can be opened.",
                    "SmartLab - Live Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!isOnline)
            {
                MessageBox.Show(
                    "This PC is not currently online.\n\n" +
                    "Wait for the PC heartbeat to return, then try again.",
                    "SmartLab - Live Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            LiveScreenWindow window =
                new LiveScreenWindow(
                    _selectedPc.PcId,
                    _selectedPc.PcNumber);

            window.Owner = this;
            window.Show();
        }


        private void ActivityLogsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ActivityLogsSection != null)
                ActivityLogsSection.BringIntoView();
        }



        private void ShowSelectedPcSidebar()
        {
            if (RightSidebarColumn != null)
            {
                RightSidebarColumn.Width =
                    new GridLength(285);
            }

            if (SelectedPcSidebar != null)
            {
                SelectedPcSidebar.Visibility =
                    Visibility.Visible;
            }
        }


        private void HideSelectedPcSidebar()
        {
            _selectedPc = null;

            if (SelectedPcSidebar != null)
            {
                SelectedPcSidebar.Visibility =
                    Visibility.Collapsed;
            }

            if (RightSidebarColumn != null)
            {
                RightSidebarColumn.Width =
                    new GridLength(0);
            }
        }


        // ==========================================
        // SHOW PC DETAILS
        // ==========================================

        private void ShowPCDetails(
            PCInfo pc)
        {
            Window detailsWindow =
                new Window
                {
                    Title =
                        $"{pc.PcNumber} - PC Details",

                    Width = 500,

                    Height = 560,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    ResizeMode =
                        ResizeMode.NoResize,

                    Owner = this,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                7,
                                16,
                                25))
                };


            // ==========================================
            // MAIN PANEL
            // ==========================================

            StackPanel panel =
                new StackPanel
                {
                    Margin =
                        new Thickness(30)
                };


            // ==========================================
            // TITLE
            // ==========================================

            TextBlock title =
                new TextBlock
                {
                    Text =
                        $"{pc.PcNumber} Details",

                    FontSize =
                        28,

                    FontWeight =
                        FontWeights.Bold,

                    Foreground =
                        Brushes.White,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            25)
                };


            panel.Children.Add(title);


            // ==========================================
            // STATUS
            // ==========================================

            panel.Children.Add(
                CreateDetailRow(
                    "Status",
                    pc.Status.ToUpper()));


            // ==========================================
            // ENABLED
            // ==========================================

            panel.Children.Add(
                CreateDetailRow(
                    "Enabled",
                    pc.IsEnabled
                        ? "YES"
                        : "NO"));


            // ==========================================
            // STUDENT
            // ==========================================

            string studentName;

            if (!string.IsNullOrWhiteSpace(
                pc.Username))
            {
                studentName =
                    pc.Username;
            }
            else if (
                pc.CurrentUserId.HasValue)
            {
                studentName =
                    $"User ID: {pc.CurrentUserId}";
            }
            else
            {
                studentName =
                    "None";
            }


            panel.Children.Add(
                CreateDetailRow(
                    "Student",
                    studentName));


            // ==========================================
            // CONNECTION
            // ==========================================

            string connectionStatus =
                "NOT AVAILABLE";


            if (pc.Status.Equals(
                "Occupied",
                StringComparison.OrdinalIgnoreCase)
                ||
                pc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (pc.LastSeen.HasValue)
                {
                    TimeSpan age =
                        DateTime.Now -
                        pc.LastSeen.Value;

                    connectionStatus =
                        age.TotalSeconds <= 10
                            ? "ONLINE"
                            : "NO RECENT HEARTBEAT";
                }
                else
                {
                    connectionStatus =
                        "NO HEARTBEAT";
                }
            }
            else if (
                pc.Status.Equals(
                    "Available",
                    StringComparison.OrdinalIgnoreCase))
            {
                connectionStatus =
                    "AVAILABLE";
            }
            else if (
                pc.Status.Equals(
                    "Maintenance",
                    StringComparison.OrdinalIgnoreCase))
            {
                connectionStatus =
                    "NOT AVAILABLE";
            }


            panel.Children.Add(
                CreateDetailRow(
                    "Connection",
                    connectionStatus));


            // ==========================================
            // LAST SEEN
            // ==========================================

            string lastSeenText =
                pc.LastSeen.HasValue
                    ? pc.LastSeen.Value
                        .ToString(
                            "MM/dd/yyyy HH:mm:ss")
                    : "None";


            panel.Children.Add(
                CreateDetailRow(
                    "Last Seen",
                    lastSeenText));


            // ==========================================
            // MAINTENANCE INFORMATION
            // ==========================================

            if (pc.Status.Equals(
                "Maintenance",
                StringComparison.OrdinalIgnoreCase))
            {
                string reason =
                    string.IsNullOrWhiteSpace(
                        pc.MaintenanceReason)
                        ? "No reason provided"
                        : pc.MaintenanceReason;


                panel.Children.Add(
                    CreateDetailRow(
                        "Maintenance Reason",
                        reason));


                string started =
                    pc.MaintenanceStarted.HasValue
                        ? pc.MaintenanceStarted.Value
                            .ToString(
                                "MM/dd/yyyy HH:mm:ss")
                        : "Unknown";


                panel.Children.Add(
                    CreateDetailRow(
                        "Maintenance Started",
                        started));
            }


            // ==========================================
            // CLOSE BUTTON
            // ==========================================

            Button closeButton =
                new Button
                {
                    Content =
                        "CLOSE",

                    Width =
                        110,

                    Height =
                        35,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0,
                            30,
                            0,
                            0),

                    FontWeight =
                        FontWeights.Bold
                };


            closeButton.Click +=
                (sender, e) =>
                {
                    detailsWindow.Close();
                };


            panel.Children.Add(
                closeButton);


            detailsWindow.Content =
                panel;


            detailsWindow.ShowDialog();
        }


        // ==========================================
        // CREATE DETAIL ROW
        // ==========================================

        private StackPanel CreateDetailRow(
            string label,
            string value)
        {
            StackPanel row =
                new StackPanel
                {
                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            15)
                };


            TextBlock labelText =
                new TextBlock
                {
                    Text =
                        label,

                    FontSize =
                        13,

                    FontWeight =
                        FontWeights.Bold,

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                127,
                                149,
                                168))
                };


            TextBlock valueText =
                new TextBlock
                {
                    Text =
                        value,

                    FontSize =
                        17,

                    FontWeight =
                        FontWeights.SemiBold,

                    Foreground =
                        Brushes.White,

                    TextWrapping =
                        TextWrapping.Wrap
                };


            row.Children.Add(
                labelText);

            row.Children.Add(
                valueText);


            return row;
        }



        // ==========================================
        // ADD PC
        // ==========================================

        private void AddPCButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowPCNumberDialog(
                null,
                async newPcNumber =>
                {
                    try
                    {
                        var request =
                            new PCCreateRequest
                            {
                                PCNumber = newPcNumber
                            };

                        var response =
                            await _httpClient.PostAsJsonAsync(
                                "api/PC",
                                request);

                        string responseText =
                            await response.Content
                                .ReadAsStringAsync();

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
                            $"PC {newPcNumber} was added successfully.",
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        await LoadPCs();
                        await LoadActivityLogs();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Unable to add PC.\n\n" +
                            ex.Message,
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
        }


        // ==========================================
        // EDIT PC NUMBER
        // ==========================================

        private void EditPCButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not PCInfo pc)
            {
                return;
            }

            ShowPCNumberDialog(
                pc.PcNumber,
                async newPcNumber =>
                {
                    try
                    {
                        var request =
                            new PCNumberUpdateRequest
                            {
                                PCNumber = newPcNumber
                            };

                        var response =
                            await _httpClient.PutAsJsonAsync(
                                $"api/PC/{pc.PcId}/number",
                                request);

                        string responseText =
                            await response.Content
                                .ReadAsStringAsync();

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
                            $"PC number updated to {newPcNumber}.",
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        await LoadPCs();
                        await LoadActivityLogs();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Unable to update PC number.\n\n" +
                            ex.Message,
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
        }


        // ==========================================
        // PC NUMBER DIALOG
        // ==========================================

        private void ShowPCNumberDialog(
            string? currentPcNumber,
            Func<string, Task> saveAction)
        {
            bool isEdit =
                !string.IsNullOrWhiteSpace(currentPcNumber);

            Window dialog =
                new Window
                {
                    Title = isEdit
                        ? "Edit PC"
                        : "Add PC",
                    Width = 430,
                    Height = 250,
                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,
                    ResizeMode =
                        ResizeMode.NoResize,
                    Owner = this,
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(244, 246, 249))
                };

            StackPanel panel =
                new StackPanel
                {
                    Margin = new Thickness(25)
                };

            TextBlock title =
                new TextBlock
                {
                    Text = isEdit
                        ? "Edit PC Number"
                        : "Add New PC",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(24, 37, 54)),
                    Margin =
                        new Thickness(0, 0, 0, 18)
                };

            TextBlock label =
                new TextBlock
                {
                    Text = "PC Number",
                    FontWeight = FontWeights.Bold,
                    Margin =
                        new Thickness(0, 0, 0, 6)
                };

            TextBox pcNumberBox =
                new TextBox
                {
                    Height = 38,
                    Padding = new Thickness(8),
                    Text = currentPcNumber ?? string.Empty
                };

            StackPanel buttons =
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    Margin =
                        new Thickness(0, 20, 0, 0)
                };

            Button cancelButton =
                new Button
                {
                    Content = "CANCEL",
                    Width = 100,
                    Height = 35,
                    Margin =
                        new Thickness(0, 0, 10, 0)
                };

            Button saveButton =
                new Button
                {
                    Content = isEdit
                        ? "SAVE"
                        : "ADD",
                    Width = 100,
                    Height = 35
                };

            cancelButton.Click +=
                (s, e) =>
                {
                    dialog.Close();
                };

            saveButton.Click +=
                async (s, e) =>
                {
                    string pcNumber =
                        pcNumberBox.Text.Trim();

                    if (string.IsNullOrWhiteSpace(pcNumber))
                    {
                        MessageBox.Show(
                            "Please enter a PC number.",
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        pcNumberBox.Focus();
                        return;
                    }

                    saveButton.IsEnabled = false;
                    cancelButton.IsEnabled = false;
                    saveButton.Content = "Saving...";

                    try
                    {
                        await saveAction(pcNumber);

                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Unable to save PC.\n\n" +
                            ex.Message,
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        saveButton.IsEnabled = true;
                        cancelButton.IsEnabled = true;
                        saveButton.Content =
                            isEdit ? "SAVE" : "ADD";
                    }
                };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(saveButton);

            panel.Children.Add(title);
            panel.Children.Add(label);
            panel.Children.Add(pcNumberBox);
            panel.Children.Add(buttons);

            dialog.Content = panel;

            dialog.ShowDialog();
        }


        // ==========================================
        // ENABLE / DISABLE PC
        // ==========================================

        private async void EnableButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not PCInfo pc)
            {
                return;
            }


            button.IsEnabled =
                false;

            button.Content =
                "Updating...";


            try
            {
                bool newEnabledStatus =
                    !pc.IsEnabled;


                var request =
                    new PCEnabledRequest
                    {
                        IsEnabled =
                            newEnabledStatus
                    };


                var response =
                    await _httpClient.PutAsJsonAsync(
                        $"api/PC/{pc.PcId}/enabled",
                        request);


                string responseText =
                    await response.Content
                        .ReadAsStringAsync();


                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    await LoadPCs();

                    return;
                }


                await LoadPCs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to update PC.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await LoadPCs();
            }
        }


        // ==========================================
        // OPEN MAINTENANCE DIALOG
        // ==========================================

        private async void MaintenanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not PCInfo pc)
            {
                return;
            }


            // ==========================================
            // MAINTENANCE WINDOW
            // ==========================================

            Window maintenanceWindow =
                new Window
                {
                    Title =
                        $"Maintenance - {pc.PcNumber}",

                    Width = 500,

                    Height = 360,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    ResizeMode =
                        ResizeMode.NoResize,

                    Owner = this,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                244,
                                246,
                                249))
                };


            // ==========================================
            // MAIN PANEL
            // ==========================================

            StackPanel panel =
                new StackPanel
                {
                    Margin =
                        new Thickness(25)
                };


            // ==========================================
            // TITLE
            // ==========================================

            TextBlock title =
                new TextBlock
                {
                    Text =
                        $"Put {pc.PcNumber} into Maintenance",

                    FontSize =
                        20,

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
                            20)
                };


            // ==========================================
            // LABEL
            // ==========================================

            TextBlock label =
                new TextBlock
                {
                    Text =
                        "Maintenance Reason",

                    FontWeight =
                        FontWeights.Bold,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            8)
                };


            // ==========================================
            // REASON TEXTBOX
            // ==========================================

            TextBox reasonBox =
                new TextBox
                {
                    Height = 100,

                    TextWrapping =
                        TextWrapping.Wrap,

                    AcceptsReturn =
                        true,

                    VerticalScrollBarVisibility =
                        ScrollBarVisibility.Auto,

                    VerticalContentAlignment =
                        VerticalAlignment.Top,

                    Padding =
                        new Thickness(8)
                };


            // ==========================================
            // BUTTON PANEL
            // ==========================================

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
                            25,
                            0,
                            0)
                };


            // ==========================================
            // CANCEL
            // ==========================================

            Button cancelButton =
                new Button
                {
                    Content =
                        "CANCEL",

                    Width =
                        100,

                    Height =
                        35,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            10,
                            0)
                };


            // ==========================================
            // SAVE
            // ==========================================

            Button saveButton =
                new Button
                {
                    Content =
                        "SAVE",

                    Width =
                        100,

                    Height =
                        35
                };


            // ==========================================
            // CANCEL CLICK
            // ==========================================

            cancelButton.Click +=
                (s, args) =>
                {
                    maintenanceWindow.Close();
                };


            // ==========================================
            // SAVE CLICK
            // ==========================================

            saveButton.Click +=
                async (s, args) =>
                {
                    string reason =
                        reasonBox.Text.Trim();


                    // ==========================================
                    // VALIDATE REASON
                    // ==========================================

                    if (string.IsNullOrWhiteSpace(
                        reason))
                    {
                        MessageBox.Show(
                            "Please enter a maintenance reason.",
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        reasonBox.Focus();

                        return;
                    }


                    saveButton.IsEnabled =
                        false;

                    cancelButton.IsEnabled =
                        false;

                    saveButton.Content =
                        "Saving...";


                    try
                    {
                        var request =
                            new PCMaintenanceRequest
                            {
                                Reason =
                                    reason
                            };


                        var response =
                            await _httpClient
                                .PutAsJsonAsync(
                                    $"api/PC/{pc.PcId}/maintenance",
                                    request);


                        string responseText =
                            await response.Content
                                .ReadAsStringAsync();


                        if (!response.IsSuccessStatusCode)
                        {
                            MessageBox.Show(
                                responseText,
                                "SmartLab",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            saveButton.IsEnabled =
                                true;

                            cancelButton.IsEnabled =
                                true;

                            saveButton.Content =
                                "SAVE";

                            return;
                        }


                        maintenanceWindow.Close();

                        await LoadPCs();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Unable to set maintenance.\n\n" +
                            ex.Message,
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        saveButton.IsEnabled =
                            true;

                        cancelButton.IsEnabled =
                            true;

                        saveButton.Content =
                            "SAVE";
                    }
                };


            // ==========================================
            // ADD BUTTONS
            // ==========================================

            buttons.Children.Add(
                cancelButton);

            buttons.Children.Add(
                saveButton);


            // ==========================================
            // ADD CONTENT
            // ==========================================

            panel.Children.Add(title);
            panel.Children.Add(label);
            panel.Children.Add(reasonBox);
            panel.Children.Add(buttons);


            maintenanceWindow.Content =
                panel;


            // ==========================================
            // SHOW DIALOG
            // ==========================================

            maintenanceWindow.ShowDialog();
        }


        // ==========================================
        // CLEAR MAINTENANCE
        // ==========================================

        private async void ClearMaintenanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not PCInfo pc)
            {
                return;
            }


            MessageBoxResult result =
                MessageBox.Show(
                    $"Clear maintenance for {pc.PcNumber}?\n\n" +
                    "The PC will become available again.",
                    "SmartLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);


            if (result !=
                MessageBoxResult.Yes)
            {
                return;
            }


            button.IsEnabled =
                false;

            button.Content =
                "Updating...";


            try
            {
                var response =
                    await _httpClient.PutAsync(
                        $"api/PC/{pc.PcId}/maintenance/clear",
                        null);


                string responseText =
                    await response.Content
                        .ReadAsStringAsync();


                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    await LoadPCs();

                    return;
                }


                await LoadPCs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to clear maintenance.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await LoadPCs();
            }
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
                        List<AnnouncementInfo>>(
                            "api/Announcement");

                var activeAnnouncements =
                    new List<AnnouncementInfo>();

                if (announcements != null)
                {
                    foreach (AnnouncementInfo announcement in announcements)
                    {
                        if (!announcement.IsActive)
                        {
                            continue;
                        }

                        if (announcement.ExpiresAt.HasValue &&
                            announcement.ExpiresAt.Value <= DateTime.Now)
                        {
                            continue;
                        }

                        activeAnnouncements.Add(announcement);
                    }
                }

                AnnouncementGrid.ItemsSource =
                    announcements ?? new List<AnnouncementInfo>();

                if (NotificationCountText != null)
                {
                    NotificationCountText.Text =
                        activeAnnouncements.Count.ToString();
                }

                if (NotificationBadge != null)
                {
                    NotificationBadge.Visibility =
                        activeAnnouncements.Count > 0
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                AnnouncementGrid.ItemsSource =
                    new List<AnnouncementInfo>();

                ApiStatusText.Text =
                    $"Announcement error: {ex.Message}";
            }
        }


        // ==========================================
        // POST ANNOUNCEMENT
        // ==========================================

        private async void PostAnnouncementButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string title =
                AnnouncementTitleBox.Text.Trim();

            string message =
                AnnouncementMessageBox.Text.Trim();

            string expirationText =
                AnnouncementExpirationBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(
                    "Please enter an announcement title.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                AnnouncementTitleBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(
                    "Please enter an announcement message.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                AnnouncementMessageBox.Focus();
                return;
            }

            DateTime? expiresAt = null;

            if (!string.IsNullOrWhiteSpace(expirationText))
            {
                if (!DateTime.TryParse(
                    expirationText,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out DateTime parsedExpiration))
                {
                    MessageBox.Show(
                        "Invalid expiration date/time.\n\n" +
                        "Example: 08/20/2026 17:00",
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    AnnouncementExpirationBox.Focus();
                    return;
                }

                expiresAt = parsedExpiration;
            }

            PostAnnouncementButton.IsEnabled = false;
            PostAnnouncementButton.Content = "POSTING...";

            try
            {
                var request =
                    new AnnouncementCreateRequest
                    {
                        Title = title,
                        Message = message,
                        ExpiresAt = expiresAt,
                        PostedByUserId = null
                    };

                var response =
                    await _httpClient.PostAsJsonAsync(
                        "api/Announcement",
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

                AnnouncementTitleBox.Clear();
                AnnouncementMessageBox.Clear();
                AnnouncementExpirationBox.Clear();

                await LoadAnnouncements();
                await LoadActivityLogs();

                MessageBox.Show(
                    "Announcement posted successfully.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to post announcement.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                PostAnnouncementButton.IsEnabled = true;
                PostAnnouncementButton.Content =
                    "POST ANNOUNCEMENT";
            }
        }


        // ==========================================
        // DEACTIVATE ANNOUNCEMENT
        // ==========================================

        private async void DeactivateAnnouncementButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not AnnouncementInfo announcement)
            {
                return;
            }

            if (!announcement.IsActive)
            {
                MessageBox.Show(
                    "This announcement is already inactive.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Deactivate announcement \"{announcement.Title}\"?",
                    "SmartLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            button.IsEnabled = false;
            button.Content = "UPDATING...";

            try
            {
                var response =
                    await _httpClient.PutAsync(
                        $"api/Announcement/{announcement.AnnouncementId}/deactivate",
                        null);

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

                await LoadAnnouncements();
                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to deactivate announcement.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "DEACTIVATE";
            }
        }


        // ==========================================
        // DELETE ANNOUNCEMENT
        // ==========================================

        private async void DeleteAnnouncementButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not AnnouncementInfo announcement)
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Delete announcement \"{announcement.Title}\"?\n\n" +
                    "This action cannot be undone.",
                    "SmartLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            button.IsEnabled = false;
            button.Content = "DELETING...";

            try
            {
                var response =
                    await _httpClient.DeleteAsync(
                        $"api/Announcement/{announcement.AnnouncementId}");

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

                await LoadAnnouncements();
                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to delete announcement.\n\n" +
                    ex.Message,
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "DELETE";
            }
        }


        // ==========================================
        // MANUAL REFRESH
        // ==========================================

        private async void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadPCs();
            await LoadActivityLogs();
            await LoadUsers();
            await LoadAnnouncements();
            await LoadServiceDeskTickets();
        }



        // ==========================================
        // LOAD SERVICE DESK TICKETS
        // ==========================================

        private async Task LoadServiceDeskTickets()
        {
            try
            {
                var response =
                    await _httpClient.GetAsync(
                        "api/AdminServiceDesk");

                string responseText =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        $"Service Desk API Error\n\n" +
                        $"Status: {(int)response.StatusCode} {response.StatusCode}\n\n" +
                        responseText,
                        "SmartLab - Service Desk",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ServiceDeskGrid.ItemsSource =
                        new List<AdminServiceDeskTicket>();

                    ApiStatusText.Text =
                        $"Service Desk API error: {(int)response.StatusCode} {response.StatusCode}";

                    return;
                }

                var tickets =
                    System.Text.Json.JsonSerializer.Deserialize<
                        List<AdminServiceDeskTicket>>(
                            responseText,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                ServiceDeskGrid.ItemsSource =
                    tickets ?? new List<AdminServiceDeskTicket>();

                ApiStatusText.Text =
                    $"Service Desk: {tickets?.Count ?? 0} ticket(s) loaded.";
            }
            catch (Exception ex)
            {
                ServiceDeskGrid.ItemsSource =
                    new List<AdminServiceDeskTicket>();

                MessageBox.Show(
                    "Unable to load Service Desk tickets.\n\n" +
                    ex.ToString(),
                    "SmartLab - Service Desk",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ApiStatusText.Text =
                    $"Service Desk error: {ex.Message}";
            }
        }


        // ==========================================
        // REFRESH SERVICE DESK
        // ==========================================

        private async void RefreshServiceDeskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadServiceDeskTickets();
        }



        // ==========================================
        // RESPOND TO SERVICE DESK TICKET
        // ==========================================

        private async void RespondServiceDeskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not AdminServiceDeskTicket ticket)
            {
                return;
            }

            await ShowServiceDeskResponseDialog(
                ticket,
                resolve: false);
        }


        // ==========================================
        // RESOLVE SERVICE DESK TICKET
        // ==========================================

        private async void ResolveServiceDeskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not AdminServiceDeskTicket ticket)
            {
                return;
            }

            await ShowServiceDeskResponseDialog(
                ticket,
                resolve: true);
        }


        // ==========================================
        // SERVICE DESK RESPONSE DIALOG
        // ==========================================

        private async Task ShowServiceDeskResponseDialog(
            AdminServiceDeskTicket ticket,
            bool resolve)
        {
            string actionTitle =
                resolve
                    ? "Resolve Service Desk Request"
                    : "Reply to Teacher";

            string defaultText =
                ticket.ResolutionNotes ?? string.Empty;

            Window dialog = new Window
            {
                Title = $"SmartLab - {actionTitle}",
                Width = 520,
                Height = 330,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = Brushes.White
            };

            StackPanel panel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            panel.Children.Add(
                new TextBlock
                {
                    Text =
                        $"{ticket.TeacherUsername} - {ticket.Subject}",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(24, 37, 54)),
                    Margin =
                        new Thickness(0, 0, 0, 10)
                });

            panel.Children.Add(
                new TextBlock
                {
                    Text = resolve
                        ? "Final resolution / message"
                        : "Message to teacher",
                    FontWeight = FontWeights.Bold,
                    Margin =
                        new Thickness(0, 0, 0, 5)
                });

            TextBox messageBox = new TextBox
            {
                Height = 130,
                Text = defaultText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                Padding = new Thickness(8),
                Margin =
                    new Thickness(0, 0, 0, 15)
            };

            panel.Children.Add(messageBox);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

            Button cancelButton = new Button
            {
                Content = "CANCEL",
                Width = 95,
                Height = 35,
                Margin =
                    new Thickness(0, 0, 10, 0)
            };

            cancelButton.Click +=
                (s, e) =>
                {
                    dialog.Close();
                };

            buttons.Children.Add(cancelButton);

            Button saveButton = new Button
            {
                Content = resolve
                    ? "RESOLVE"
                    : "SEND REPLY",
                Width = 120,
                Height = 35,
                FontWeight = FontWeights.Bold
            };

            saveButton.Click += async (s, e) =>
            {
                string message =
                    messageBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(message))
                {
                    MessageBox.Show(
                        "Please enter a message.",
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    messageBox.Focus();
                    return;
                }

                saveButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                saveButton.Content =
                    resolve
                        ? "RESOLVING..."
                        : "SENDING...";

                try
                {
                    var request =
                        new ServiceDeskUpdateRequest
                        {
                            Status = resolve
                                ? "Resolved"
                                : "In Progress",
                            Message = message,
                            AssignedToUsername =
                                AdminNameText.Text,
                            Resolved = resolve
                        };

                    var response =
                        await _httpClient.PutAsJsonAsync(
                            $"api/AdminServiceDesk/{ticket.ServiceDeskTicketId}",
                            request);

                    string responseText =
                        await response.Content
                            .ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(
                            responseText,
                            "SmartLab",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    dialog.Close();

                    await LoadServiceDeskTickets();
                    await LoadActivityLogs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to update Service Desk request.\n\n" +
                        ex.Message,
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    saveButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                    saveButton.Content =
                        resolve
                            ? "RESOLVE"
                            : "SEND REPLY";
                }
            };

            buttons.Children.Add(saveButton);
            panel.Children.Add(buttons);

            dialog.Content = panel;

            dialog.ShowDialog();
        }


        // ==========================================
        // REFRESH ACTIVITY LOGS
        // ==========================================

        private async void RefreshLogsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadActivityLogs();
        }


        // ==========================================
        // ADMIN LOGOUT
        // ==========================================

        private void LogoutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _refreshTimer.Stop();



            // ==========================================
            // CLEAR AUTHENTICATED SESSION
            // ==========================================

            AuthSession.Clear();

            MainWindow loginWindow =
                            new MainWindow();

            loginWindow.Show();

            Close();
        }


        // ==========================================
        // WINDOW CLOSED
        // ==========================================

        protected override void OnClosed(
            EventArgs e)
        {
            _refreshTimer.Stop();

            _httpClient.Dispose();

            base.OnClosed(e);
        }
    }



    // ==========================================
    // ADMIN SERVICE DESK INFORMATION
    // ==========================================

    public class AdminServiceDeskTicket
    {
        public int ServiceDeskTicketId { get; set; }

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

        public string Status { get; set; } =
            "Open";

        public int? AssignedToUserId { get; set; }

        public string? AssignedToUsername { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolutionNotes { get; set; }
    }



    // ==========================================
    // SERVICE DESK UPDATE REQUEST
    // ==========================================

    public class ServiceDeskUpdateRequest
    {
        public string Status { get; set; } =
            "In Progress";

        public string Message { get; set; } =
            string.Empty;

        public string? AssignedToUsername { get; set; }

        public bool Resolved { get; set; }
    }


    // ==========================================
    // PC INFORMATION
    // ==========================================

    public class PCInfo
    {
        public int PcId { get; set; }

        public string PcNumber { get; set; } =
            string.Empty;

        public string Status { get; set; } =
            string.Empty;

        public int? CurrentUserId { get; set; }

        public string? Username { get; set; }

        public DateTime? LastSeen { get; set; }


        // ==========================================
        // ENABLE / DISABLE
        // ==========================================

        public bool IsEnabled { get; set; }


        // ==========================================
        // MAINTENANCE
        // ==========================================

        public string? MaintenanceReason { get; set; }

        public DateTime? MaintenanceStarted { get; set; }
    }


    // ==========================================
    // ENABLE / DISABLE REQUEST
    // ==========================================

    public class PCEnabledRequest
    {
        public bool IsEnabled { get; set; }
    }


    // ==========================================
    // MAINTENANCE REQUEST
    // ==========================================

    public class PCMaintenanceRequest
    {
        public string Reason { get; set; } =
            string.Empty;
    }

    // ==========================================
    // ADD PC REQUEST
    // ==========================================

    public class PCCreateRequest
    {
        public string PCNumber { get; set; } =
            string.Empty;
    }


    // ==========================================
    // UPDATE PC NUMBER REQUEST
    // ==========================================

    public class PCNumberUpdateRequest
    {
        public string PCNumber { get; set; } =
            string.Empty;
    }


    // ==========================================
    // USER INFORMATION
    // ==========================================

    public class UserInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UserCreateRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";
        public string PerformedBy { get; set; } = string.Empty;
    }

    public class UserRoleUpdateRequest
    {
        public string Role { get; set; } = "Student";
        public string PerformedBy { get; set; } = string.Empty;
    }

    public class UserPasswordResetRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
    }


    // ==========================================
    // ANNOUNCEMENT INFORMATION
    // ==========================================

    public class AnnouncementInfo
    {
        public int AnnouncementId { get; set; }

        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public int? PostedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }


    // ==========================================
    // CREATE ANNOUNCEMENT REQUEST
    // ==========================================

    public class AnnouncementCreateRequest
    {
        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public int? PostedByUserId { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }


    // ==========================================
    // ACTIVITY LOG INFORMATION
    // ==========================================

    public class ActivityLogInfo
    {
        public int ActivityLogId { get; set; }
        public int? PcId { get; set; }
        public string? PcNumber { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}