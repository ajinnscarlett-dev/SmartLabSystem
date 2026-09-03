using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

namespace SmartLab.Client
{
    public partial class AdminLoginWindow : Window
    {
        private readonly HttpClient _httpClient;

        public AdminLoginWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri(
                        SmartLabServerConfig.BaseUrl)
            };

            // ==========================================
            // APPLY EXISTING AUTH SESSION
            // ==========================================

            AuthSession.Apply(
                _httpClient);
        }

        // ==========================================
        // ADMIN LOGIN
        // ==========================================

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text =
                "Finding SmartLab Server...";

            LoginButton.IsEnabled = false;

            // ==========================================
            // RESOLVE CURRENT SERVER IP
            // ==========================================

            bool serverFound =
                await SmartLabServerConfig
                    .ResolveServerAsync();

            if (!serverFound)
            {
                StatusText.Text =
                    "SmartLab Server could not be found on the local network.";

                LoginButton.IsEnabled = true;

                return;
            }

            // IMPORTANT:
            // SmartLabServerConfig.BaseUrl may have changed
            // after LAN discovery, so update this client too.

            _httpClient.BaseAddress =
                new Uri(
                    SmartLabServerConfig.BaseUrl);

            StatusText.Text =
                "Logging in...";

            var loginData = new
            {
                username =
                    UsernameTextBox.Text.Trim(),

                password =
                    PasswordBox.Password
            };

            try
            {
                // ==========================================
                // SEND LOGIN REQUEST
                // ==========================================

                var response =
                    await _httpClient.PostAsJsonAsync(
                        "api/Auth/login",
                        loginData
                    );

                var responseText =
                    await response.Content
                        .ReadAsStringAsync();

                // ==========================================
                // LOGIN FAILED
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    StatusText.Text =
                        "Invalid admin username or password.";

                    LoginButton.IsEnabled = true;

                    AuthSession.Clear();

                    return;
                }

                // ==========================================
                // READ LOGIN RESPONSE
                // ==========================================

                using JsonDocument json =
                    JsonDocument.Parse(
                        responseText
                    );

                int userId =
                    json.RootElement
                        .GetProperty("userId")
                        .GetInt32();

                string username =
                    json.RootElement
                        .GetProperty("username")
                        .GetString()
                        ?? "";

                string role =
                    json.RootElement
                        .GetProperty("role")
                        .GetString()
                        ?? "";

                string token =
                    json.RootElement
                        .GetProperty("token")
                        .GetString()
                        ?? "";

                // ==========================================
                // VERIFY ADMIN ROLE
                // ==========================================

                if (!role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text =
                        "Access denied. Admin account required.";

                    LoginButton.IsEnabled = true;

                    AuthSession.Clear();

                    return;
                }

                // ==========================================
                // SAVE AUTHENTICATED SESSION
                // ==========================================

                AuthSession.SetSession(
                    token,
                    userId,
                    username,
                    role);

                // ==========================================
                // APPLY JWT TO HTTP CLIENT
                // ==========================================

                AuthSession.Apply(
                    _httpClient);

                // ==========================================
                // OPEN ADMIN DASHBOARD
                // ==========================================

                AdminDashboard dashboard =
                    new AdminDashboard(
                        username
                    );

                dashboard.Show();

                // ==========================================
                // CLOSE ADMIN LOGIN
                // ==========================================

                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Connection error: {ex.Message}";

                LoginButton.IsEnabled = true;

                AuthSession.Clear();
            }
        }

        // ==========================================
        // CLEANUP
        // ==========================================

        protected override void OnClosed(
            EventArgs e)
        {
            _httpClient.Dispose();

            base.OnClosed(e);
        }
    }
}
