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
            // GET THIS COMPUTER'S SMARTLAB PC NUMBER
            // ==========================================

            _pcNumber = GetSmartLabPcNumber();
        }


        // ==========================================
        // GET SMARTLAB PC NUMBER
        // ==========================================
        //
        // Examples:
        //
        // Windows name:
        //     601-PC01
        // Result:
        //     601-PC01
        //
        // Windows name:
        //     PC01
        // Result:
        //     601-PC01
        //
        // Windows name:
        //     PC02
        // Result:
        //     601-PC02
        //
        // ==========================================

        private string GetSmartLabPcNumber()
        {
            string machineName =
                Environment.MachineName.Trim();


            // ==========================================
            // ALREADY USING SMARTLAB FORMAT
            // ==========================================

            if (machineName.StartsWith(
                    "601-PC",
                    StringComparison.OrdinalIgnoreCase))
            {
                return machineName.ToUpper();
            }


            // ==========================================
            // SIMPLE PC FORMAT
            // Example: PC01
            // ==========================================

            if (machineName.StartsWith(
                    "PC",
                    StringComparison.OrdinalIgnoreCase))
            {
                string numberPart =
                    machineName.Substring(2);


                if (int.TryParse(
                        numberPart,
                        out int pcNumber))
                {
                    return $"601-PC{pcNumber:00}";
                }
            }


            // ==========================================
            // UNKNOWN FORMAT
            //
            // Do not guess.
            // Keep the actual Windows computer name.
            // ==========================================

            return machineName;
        }


        // ==========================================
        // STUDENT LOGIN
        // ==========================================

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
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


                        return;
                    }


                    // ==========================================
                    // READ PC INFORMATION
                    // ==========================================

                    using JsonDocument pcJson =
                        JsonDocument.Parse(
                            pcResponseText
                        );


                    // ==========================================
                    // GET PC ID
                    // ==========================================

                    int pcId =
                        pcJson.RootElement
                            .GetProperty("pcId")
                            .GetInt32();


                    // ==========================================
                    // GET PC NUMBER
                    // ==========================================

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
                // ADMIN LOGIN
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
                    // Normally Admin users should enter
                    // through the hidden Admin/MIS button.
                    //
                    // This check is kept here as a safety
                    // fallback.

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