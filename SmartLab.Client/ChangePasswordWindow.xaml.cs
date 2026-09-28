using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLab.Client;

public partial class ChangePasswordWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly string _username;

    public ChangePasswordWindow(HttpClient httpClient, string username)
    {
        InitializeComponent();
        _username = username;
        _httpClient = httpClient;
        Title = $"Change Password - {_username}";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string currentPassword = CurrentPasswordBox.Password;
        string newPassword = NewPasswordBox.Password;
        string confirmPassword = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            StatusText.Text = "Enter your current password.";
            CurrentPasswordBox.Focus();
            return;
        }

        if (newPassword.Length < 6)
        {
            StatusText.Text = "New password must be at least 6 characters.";
            NewPasswordBox.Focus();
            return;
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            StatusText.Text = "The new passwords do not match.";
            ConfirmPasswordBox.Focus();
            return;
        }

        SaveButton.IsEnabled = false;
        StatusText.Text = "Changing password...";

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/Auth/change-password",
                new
                {
                    CurrentPassword = currentPassword,
                    NewPassword = newPassword
                });

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = GetPasswordChangeErrorMessage(response.StatusCode, await response.Content.ReadAsStringAsync());
                Debug.WriteLine($"Password change rejected with HTTP {(int)response.StatusCode}.");
                SaveButton.IsEnabled = true;
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Password change HTTP error: {ex}");
            StatusText.Text = "Unable to connect to SmartLab Server.";
            SaveButton.IsEnabled = true;
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"Password change request timed out: {ex}");
            StatusText.Text = "SmartLab Server did not respond in time.";
            SaveButton.IsEnabled = true;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Password change response parsing error: {ex}");
            StatusText.Text = "SmartLab Server returned an invalid response.";
            SaveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected password change error: {ex}");
            StatusText.Text = "SmartLab Server is unavailable. Contact MIS.";
            SaveButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string GetPasswordChangeErrorMessage(HttpStatusCode statusCode, string responseText)
    {
        string? serverMessage = TryExtractServerMessage(responseText);

        if (statusCode == HttpStatusCode.BadRequest)
            return serverMessage ?? "The password change request is invalid.";

        if (statusCode == HttpStatusCode.Unauthorized)
            return serverMessage?.Contains("inactive", StringComparison.OrdinalIgnoreCase) == true
                ? "This account is inactive. Contact MIS."
                : "Your session is no longer valid. Please sign in again.";

        if (statusCode == HttpStatusCode.Forbidden)
            return serverMessage ?? "You must change your password before continuing.";

        if ((int)statusCode >= 500)
            return "SmartLab Server is unavailable. Contact MIS.";

        return serverMessage ?? "Password change could not be completed.";
    }

    private static string? TryExtractServerMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("message", out JsonElement message) &&
                message.ValueKind == JsonValueKind.String)
            {
                string value = message.GetString()?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

}
