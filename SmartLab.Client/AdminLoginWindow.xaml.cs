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
                BaseAddress = new Uri("https://localhost:7277/")
            };
        }


        // ==========================================
        // ADMIN LOGIN
        // ==========================================

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text = "Logging in...";
            LoginButton.IsEnabled = false;

            var loginData = new
            {
                username = UsernameTextBox.Text.Trim(),
                password = PasswordBox.Password
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
                    await response.Content.ReadAsStringAsync();


                // ==========================================
                // LOGIN FAILED
                // ==========================================

                if (!response.IsSuccessStatusCode)
                {
                    StatusText.Text =
                        "Invalid admin username or password.";

                    LoginButton.IsEnabled = true;
                    return;
                }


                // ==========================================
                // READ LOGIN RESPONSE
                // ==========================================

                using JsonDocument json =
                    JsonDocument.Parse(responseText);

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
                    return;
                }


                // ==========================================
                // OPEN ADMIN DASHBOARD
                // ==========================================

                AdminDashboard dashboard =
                    new AdminDashboard(username);

                dashboard.Show();

                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Connection error: {ex.Message}";

                LoginButton.IsEnabled = true;
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