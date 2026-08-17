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

        // ==========================================
        // THIS COMPUTER'S SMARTLAB PC NUMBER
        // ==========================================

        private readonly string _pcNumber;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri("https://localhost:7277/")
            };

            // ==========================================
            // AUTH SESSION
            // ==========================================

            // If a token already exists, attach it.
            // Login itself is allowed anonymously, so
            // this does not prevent the login request.

            AuthSession.Apply(_httpClient);

            // ==========================================
            // AUTO-DETECT THIS COMPUTER
            // ==========================================

            _pcNumber =
                PCConfig.PCNumber;
        }

        // ==========================================
        // STUDENT / TEACHER / ADMIN LOGIN
        // ==========================================

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text =
                $"Connecting to {_pcNumber}...";

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
                        "Invalid username or password.";

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

                // ==========================================
                // GET JWT TOKEN
                // ==========================================

                string token =
                    json.RootElement
                        .GetProperty("token")
                        .GetString()
                        ?? "";

                // ==========================================
                // SAVE AUTHENTICATED SESSION
                // ==========================================

                AuthSession.SetSession(
                    token,
                    userId,
                    username,
                    role);

                // ==========================================
                // ATTACH JWT TO FUTURE API REQUESTS
                // ==========================================

                AuthSession.Apply(_httpClient);

                // ==========================================
                // STUDENT LOGIN
                // ==========================================

                if (role.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text =
                        $"Connecting to {_pcNumber}...";

                    // ==========================================
                    // LOGIN TO THIS PHYSICAL PC
                    // ==========================================

                    var pcResponse =
                        await _httpClient.PostAsync(
                            $"api/PC/login/{Uri.EscapeDataString(_pcNumber)}/{userId}",
                            null
                        );

                    var pcResponseText =
                        await pcResponse.Content
                            .ReadAsStringAsync();

                    // ==========================================
                    // PC LOGIN FAILED
                    // ==========================================

                    if (!pcResponse.IsSuccessStatusCode)
                    {
                        try
                        {
                            using JsonDocument pcError =
                                JsonDocument.Parse(
                                    pcResponseText
                                );

                            string message =
                                pcError.RootElement
                                    .GetProperty("message")
                                    .GetString()
                                    ??
                                    $"Unable to login to {_pcNumber}.";

                            StatusText.Text =
                                message;
                        }
                        catch
                        {
                            StatusText.Text =
                                $"Unable to login to {_pcNumber}.";
                        }

                        AuthSession.Clear();

                        return;
                    }

                    // ==========================================
                    // READ PC INFORMATION
                    // ==========================================

                    using JsonDocument pcJson =
                        JsonDocument.Parse(
                            pcResponseText
                        );

                    int pcId =
                        pcJson.RootElement
                            .GetProperty("pcId")
                            .GetInt32();

                    string pcNumber =
                        pcJson.RootElement
                            .GetProperty("pcNumber")
                            .GetString()
                            ?? _pcNumber;

                    // ==========================================
                    // OPEN SMARTLAB WIDGET
                    // ==========================================

                    SmartLabWidget widget =
                        new SmartLabWidget(
                            username,
                            pcNumber,
                            userId,
                            pcId
                        );

                    widget.Show();

                    // ==========================================
                    // CLOSE LOGIN WINDOW
                    // ==========================================

                    Close();
                }

                // ==========================================
                // TEACHER LOGIN
                // ==========================================

                else if (role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TeacherDashboard dashboard =
                        new TeacherDashboard(
                            username,
                            userId
                        );

                    dashboard.Show();

                    Close();
                }

                // ==========================================
                // ADMIN LOGIN
                // ==========================================

                else if (role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AdminDashboard dashboard =
                        new AdminDashboard(
                            username
                        );

                    dashboard.Show();

                    Close();
                }

                // ==========================================
                // OTHER ROLE
                // ==========================================

                else
                {
                    StatusText.Text =
                        $"Login successful!\n" +
                        $"Welcome, {username}!\n" +
                        $"Role: {role}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Connection error: {ex.Message}";

                AuthSession.Clear();
            }
        }

        // ==========================================
        // ADMIN / MANAGEMENT LOGIN
        // ==========================================

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
