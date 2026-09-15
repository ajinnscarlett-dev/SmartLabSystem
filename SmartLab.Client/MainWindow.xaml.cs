using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

namespace SmartLab.Client
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _pcNumber;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient();
            AuthSession.Apply(_httpClient);
            _pcNumber = PCConfig.PCNumber;
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text = "Finding SmartLab Server...";
            LoginButton.IsEnabled = false;

            bool serverFound =
                await SmartLabServerConfig.ResolveServerAsync();

            if (!serverFound)
            {
                StatusText.Text =
                    "SmartLab Server could not be found on the local network.";
                LoginButton.IsEnabled = true;
                return;
            }

            StatusText.Text =
                $"Connecting to {_pcNumber}...";

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
                        "Invalid username or password.";

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
                    ?? string.Empty;

                string role =
                    json.RootElement
                        .GetProperty("role")
                        .GetString()
                    ?? string.Empty;

                string token =
                    json.RootElement
                        .GetProperty("token")
                        .GetString()
                    ?? string.Empty;

                AuthSession.SetSession(
                    token,
                    userId,
                    username,
                    role);

                // Refresh this HttpClient's Authorization header immediately
                // after receiving the JWT. The same client is then used for the
                // protected PC login request below.
                AuthSession.Apply(_httpClient);

                if (role.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text =
                        $"Connecting to {_pcNumber}...";

                    var pcUri =
                        new Uri(
                            new Uri(SmartLabServerConfig.BaseUrl),
                            $"api/PC/login/{Uri.EscapeDataString(_pcNumber)}/{userId}");

                    var pcResponse =
                        await _httpClient.PostAsync(pcUri, null);

                    string pcResponseText =
                        await pcResponse.Content.ReadAsStringAsync();

                    if (!pcResponse.IsSuccessStatusCode)
                    {
                        string message =
                            $"Unable to login to {_pcNumber}.";

                        try
                        {
                            using JsonDocument pcError =
                                JsonDocument.Parse(pcResponseText);

                            message =
                                pcError.RootElement
                                    .GetProperty("message")
                                    .GetString()
                                ?? message;
                        }
                        catch
                        {
                        }

                        StatusText.Text = message;
                        AuthSession.Clear();
                        AuthSession.Apply(_httpClient);
                        LoginButton.IsEnabled = true;
                        return;
                    }

                    using JsonDocument pcJson =
                        JsonDocument.Parse(pcResponseText);

                    int pcId =
                        pcJson.RootElement
                            .GetProperty("pcId")
                            .GetInt32();

                    string pcNumber =
                        pcJson.RootElement
                            .GetProperty("pcNumber")
                            .GetString()
                    ?? _pcNumber;

                    SmartLabWidget widget =
                        new SmartLabWidget(
                            username,
                            pcNumber,
                            userId,
                            pcId);

                    widget.Show();
                    Close();
                }
                else if (role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TeacherDashboard dashboard =
                        new TeacherDashboard(
                            username,
                            userId);

                    dashboard.Show();
                    Close();
                }
                else if (role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AdminDashboard dashboard =
                        new AdminDashboard(username);

                    dashboard.Show();
                    Close();
                }
                else
                {
                    AuthSession.Clear();
                    AuthSession.Apply(_httpClient);
                    StatusText.Text =
                        $"Login successful, but role '{role}' is not supported by this client.";
                    LoginButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Connection error: {ex.Message}";
                LoginButton.IsEnabled = true;
                AuthSession.Clear();
                AuthSession.Apply(_httpClient);
            }
        }

        private void AdminLoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                AdminLoginWindow adminLogin =
                    new AdminLoginWindow();

                adminLogin.Show();
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Unable to open Admin Login: {ex.Message}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient.Dispose();
            base.OnClosed(e);
        }
    }
}
