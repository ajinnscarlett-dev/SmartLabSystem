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

            _httpClient = new HttpClient();

            AuthSession.Apply(_httpClient);
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text =
                "Finding SmartLab Server...";

            LoginButton.IsEnabled = false;

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

            StatusText.Text = "Logging in...";

            var loginData = new
            {
                username = UsernameTextBox.Text.Trim(),
                password = PasswordBox.Password
            };

            try
            {
                Uri loginUri =
                    new Uri(
                        new Uri(SmartLabServerConfig.BaseUrl),
                        "api/Auth/login");

                var response =
                    await _httpClient.PostAsJsonAsync(
                        loginUri,
                        loginData);

                string responseText =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string message =
                        "Invalid admin username or password.";

                    try
                    {
                        using JsonDocument errorJson =
                            JsonDocument.Parse(responseText);

                        message =
                            errorJson.RootElement
                                .GetProperty("message")
                                .GetString()
                            ?? message;
                    }
                    catch
                    {
                    }

                    StatusText.Text = message;
                    LoginButton.IsEnabled = true;
                    AuthSession.Clear();
                    return;
                }

                using JsonDocument json =
                    JsonDocument.Parse(responseText);

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

                AuthSession.SetSession(
                    token,
                    userId,
                    username,
                    role);

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
                AuthSession.Clear();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient.Dispose();
            base.OnClosed(e);
        }
    }
}
