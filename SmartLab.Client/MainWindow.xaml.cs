using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

            AuthSession.Apply(_httpClient);

            // ==========================================
            // AUTO-DETECT THIS COMPUTER
            // ==========================================

            _pcNumber =
                PCConfig.PCNumber;
        }

        // ==========================================
        // GET ACTIVE MAC ADDRESS
        // ==========================================

        private static string? GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface networkInterface
                    in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus !=
                        OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    PhysicalAddress physicalAddress =
                        networkInterface.GetPhysicalAddress();

                    byte[] bytes =
                        physicalAddress.GetAddressBytes();

                    if (bytes.Length == 6)
                    {
                        return string.Join(
                            "-",
                            bytes.Select(
                                b => b.ToString("X2")));
                    }
                }
            }
            catch
            {
                // Network identity is best-effort.
            }

            return null;
        }

        // ==========================================
        // GET ACTIVE LOCAL IPV4
        // ==========================================

        private static string? GetLocalIPv4Address()
        {
            try
            {
                foreach (NetworkInterface networkInterface
                    in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus !=
                        OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties =
                        networkInterface.GetIPProperties();

                    foreach (UnicastIPAddressInformation address
                        in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily ==
                            AddressFamily.InterNetwork)
                        {
                            string ip =
                                address.Address.ToString();

                            if (!ip.StartsWith("169.254."))
                            {
                                return ip;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Network identity is best-effort.
            }

            return null;
        }

        // ==========================================
        // REGISTER THIS PC'S NETWORK IDENTITY
        // ==========================================

        private async Task<bool> RegisterThisPcNetworkIdentity(
            int userId)
        {
            string? macAddress =
                GetMacAddress();

            string? ipAddress =
                GetLocalIPv4Address();

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                StatusText.Text =
                    "PC connected, but MAC address could not be detected.";

                return false;
            }

            var registrationData = new
            {
                userId,
                pcNumber = _pcNumber,
                macAddress,
                ipAddress
            };

            try
            {
                var response =
                    await _httpClient.PostAsJsonAsync(
                        "api/PCRegistration/register",
                        registrationData
                    );

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                string errorText =
                    await response.Content
                        .ReadAsStringAsync();

                try
                {
                    using JsonDocument errorJson =
                        JsonDocument.Parse(errorText);

                    string message =
                        errorJson.RootElement
                            .GetProperty("message")
                            .GetString()
                            ??
                            "PC network registration failed.";

                    StatusText.Text =
                        message;
                }
                catch
                {
                    StatusText.Text =
                        "PC network registration failed.";
                }

                return false;
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"PC network registration error: {ex.Message}";

                return false;
            }
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
                    // REGISTER MAC + IP
                    // ==========================================

                    StatusText.Text =
                        "Registering PC network identity...";

                    await RegisterThisPcNetworkIdentity(
                        userId
                    );

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
