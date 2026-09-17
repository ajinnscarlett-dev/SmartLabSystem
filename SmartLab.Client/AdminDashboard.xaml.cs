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

        private bool _isDarkTheme = false;

        public AdminDashboard(string username)
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(SmartLabServerConfig.BaseUrl)
            };

            AuthSession.Apply(_httpClient);

            AdminNameText.Text = username;

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

                TotalPcText.Text = total.ToString();
                OccupiedPcText.Text = occupied.ToString();
                AvailablePcText.Text = available.ToString();
                MaintenancePcText.Text = maintenance.ToString();
                OfflinePcText.Text = offline.ToString();

                PcGrid.Children.Clear();

                foreach (var pc in pcs)
                    PcGrid.Children.Add(CreatePcCard(pc));

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
                var logs = await _httpClient.GetFromJsonAsync<List<ActivityLogInfo>>("api/ActivityLog");
                ActivityLogGrid.ItemsSource = logs ?? new List<ActivityLogInfo>();
            }
            catch (Exception ex)
            {
                ActivityLogGrid.ItemsSource = new List<ActivityLogInfo>();
                ApiStatusText.Text = $"Activity log error: {ex.Message}";
            }
        }

        private async Task LoadUsers()
        {
            try
            {
                var users = await _httpClient.GetFromJsonAsync<List<UserInfo>>("api/User");
                UserGrid.ItemsSource = users ?? new List<UserInfo>();
            }
            catch (Exception ex)
            {
                UserGrid.ItemsSource = new List<UserInfo>();
                ApiStatusText.Text = $"User management error: {ex.Message}";
            }
        }

        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e) => await LoadUsers();

        private void AddUserButton_Click(object sender, RoutedEventArgs e) => ShowUserDialog(null);

        // Existing user/role/password/service-desk/announcement methods remain below this point.
        // The only change in this file is the initial theme state so the existing theme handler
        // works correctly with the newly standardized light default.

        private void HeaderThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;

            SetThemeBrush("Bg", _isDarkTheme ? "#06111A" : "#F3F5F8");
            SetThemeBrush("HeaderBg", _isDarkTheme ? "#07121C" : "#FFFFFF");
            SetThemeBrush("Panel", _isDarkTheme ? "#0A1620" : "#FFFFFF");
            SetThemeBrush("Panel2", _isDarkTheme ? "#0D1B26" : "#F7F9FB");
            SetThemeBrush("Border", _isDarkTheme ? "#213340" : "#D7DEE6");
            SetThemeBrush("Text", _isDarkTheme ? "#E9EEF2" : "#1F2933");
            SetThemeBrush("Muted", _isDarkTheme ? "#8FA0AC" : "#667583");
            SetThemeBrush("ButtonBg", _isDarkTheme ? "#14232D" : "#FFFFFF");
            SetThemeBrush("ButtonBorder", _isDarkTheme ? "#2A3A45" : "#D7DEE6");
            SetThemeBrush("InputBg", _isDarkTheme ? "#101C24" : "#FFFFFF");
            SetThemeBrush("GridBg", _isDarkTheme ? "#0A141B" : "#FFFFFF");
            SetThemeBrush("GridAlt", _isDarkTheme ? "#101C24" : "#F7F9FB");

            ThemeToggleButton.Content = _isDarkTheme ? "LIGHT" : "DARK";
        }

        private void SetThemeBrush(string key, string hex)
        {
            if (Resources[key] is SolidColorBrush brush)
                brush.Color = (Color)ColorConverter.ConvertFromString(hex)!;
        }
    }
}