using System;
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
                StatusText.Text = await ReadMessageAsync(response);
                SaveButton.IsEnabled = true;
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unable to change password: {ex.Message}";
            SaveButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();

        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("message", out JsonElement message))
            {
                return message.GetString() ?? text;
            }
        }
        catch
        {
        }

        return string.IsNullOrWhiteSpace(text)
            ? $"Password change failed ({(int)response.StatusCode})."
            : $"Password change failed ({(int)response.StatusCode}).\n\n{text}";
    }
}
