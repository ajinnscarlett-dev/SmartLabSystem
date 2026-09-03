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
        private readonly string _pcNumber;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri(
                        SmartLabServerConfig.BaseUrl)
            };

            AuthSession.Apply(
                _httpClient);

            _pcNumber =
                PCConfig.PCNumber;
        }

        private static string? GetMacAddress()
        {
            try
            {
                foreach (
                    NetworkInterface networkInterface
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
            }

            return null;
        }

        private static string? GetLocalIPv4Address()
        {
            try
            {
                foreach (
                    NetworkInterface networkInterface
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

                    foreach (
                        UnicastIPAddressInformation address
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
            }

            return null;
        }

        private async Task<bool>
            RegisterThisPcNetworkIdentity(
                int userId)
        {
            string? macAddress =
                GetMacAddress();

            string? ipAddress =
                GetLocalIPv4Address();

            if (string.IsNullOrWhiteSpace(
                macAddress))
            {
                StatusText.Text =
                    "PC connected, but MAC address could not be detected.";

                return false;
            }

            var registrationData =
                new
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
                        registrationData);

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
                        JsonDocument.Parse(
                            errorText);

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

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            StatusText.Text =
                "Finding SmartLab Server...";

            bool serverFound =
                await SmartLabServerConfig
                    .ResolveServerAsync();

            if (!serverFound)
            {
                StatusText.Text =
                    "SmartLab Server could not be found on the local network.";

                return;
            }

            _httpClient.BaseAddress =
                new Uri(
                    SmartLabServerConfig.BaseUrl);

            StatusText.Text =
                $"Connecting to {_pcNumber}...";

            var loginData =
                new
                {
                    username =
                        UsernameTextBox.Text.Trim(),

                    password =
                        PasswordBox.Password
                };

            try
            {
                var response =
                    await _httpClient.PostAsJsonAsync(
                        "api/Auth/login",
                        loginData);

                var responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    StatusText.Text =
                        "Invalid username or password.";

                    return;
                }

                using JsonDocument json =
                    JsonDocument.Parse(
                        responseText);

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

                AuthSession.SetSession(
                    token,
                    userId,
                    username,
                    role);

                AuthSession.Apply(
                    _httpClient);

                if (role.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text =
                        $"Connecting to {_pcNumber}...";

                    StatusText.Text =
                        $"Connecting to {_pcNumber}...";

                    var pcResponse =
                        await _httpClient.PostAsync(
                            $"api/PC/login/{Uri.EscapeDataString(_pcNumber)}/{userId}",
                            null);

                    var pcResponseText =
                        await pcResponse.Content
                            .ReadAsStringAsync();

                    if (!pcResponse.IsSuccessStatusCode)
                    {
                        try
                        {
                            using JsonDocument pcError =
                                JsonDocument.Parse(
                                    pcResponseText);

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

                    using JsonDocument pcJson =
                        JsonDocument.Parse(
                            pcResponseText);

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
                        new AdminDashboard(
                            username);

                    dashboard.Show();

                    Close();
                }
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

        protected override void OnClosed(
            EventArgs e)
        {
            _httpClient.Dispose();
            base.OnClosed(e);
        }
    }
}
