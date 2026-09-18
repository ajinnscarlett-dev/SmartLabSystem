using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

namespace SmartLab.Client;

public partial class TeacherLoginWindow : Window
{
    private readonly HttpClient _httpClient = new();

    public TeacherLoginWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        AuthSession.Apply(_httpClient);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameTextBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Username and password are required.";
            return;
        }

        LoginButton.IsEnabled = false;
        StatusText.Text = "Connecting to SmartLab Server...";

        try
        {
            if (!await SmartLabServerConfig.ResolveServerAsync())
            {
                StatusText.Text = "SmartLab Server could not be found on the local network.";
                return;
            }

            _httpClient.BaseAddress = new Uri(SmartLabServerConfig.BaseUrl);
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/Auth/login",
                new { Username = username, Password = password });

            string responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = ExtractMessage(responseText, "Invalid username or password.");
                AuthSession.Clear();
                AuthSession.Apply(_httpClient);
                return;
            }

            using JsonDocument json = JsonDocument.Parse(responseText);
            int userId = json.RootElement.GetProperty("userId").GetInt32();
            string returnedUsername = json.RootElement.GetProperty("username").GetString() ?? string.Empty;
            string role = json.RootElement.GetProperty("role").GetString() ?? string.Empty;
            string token = json.RootElement.GetProperty("token").GetString() ?? string.Empty;
            bool mustChangePassword = json.RootElement.TryGetProperty("mustChangePassword", out JsonElement change)
                && change.GetBoolean();

            if (!role.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
            {
                AuthSession.Clear();
                AuthSession.Apply(_httpClient);
                StatusText.Text = "This login is for Teacher accounts only.";
                return;
            }

            AuthSession.SetSession(token, userId, returnedUsername, role);
            AuthSession.Apply(_httpClient);

            if (mustChangePassword)
            {
                var changePasswordWindow = new ChangePasswordWindow(_httpClient, returnedUsername)
                {
                    Owner = this
                };

                if (changePasswordWindow.ShowDialog() != true)
                {
                    AuthSession.Clear();
                    AuthSession.Apply(_httpClient);
                    StatusText.Text = "Password change is required before continuing.";
                    return;
                }
            }

            var dashboard = new TeacherDashboard(returnedUsername, userId);
            dashboard.Show();
            Close();
        }
        catch (Exception ex)
        {
            AuthSession.Clear();
            AuthSession.Apply(_httpClient);
            StatusText.Text = $"Connection error: {ex.Message}";
        }
        finally
        {
            if (IsVisible)
                LoginButton.IsEnabled = true;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

    private static string ExtractMessage(string text, string fallback)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            return json.RootElement.TryGetProperty("message", out JsonElement message)
                ? message.GetString() ?? fallback
                : fallback;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient.Dispose();
        base.OnClosed(e);
    }
}
