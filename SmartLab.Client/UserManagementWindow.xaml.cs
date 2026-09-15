using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client;

public partial class UserManagementWindow : Window
{
    private readonly HttpClient _httpClient;
    private List<AccountRow> _accounts = new();

    public UserManagementWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _httpClient = new HttpClient { BaseAddress = new Uri(SmartLabServerConfig.BaseUrl) };
        AuthSession.Apply(_httpClient);
        Loaded += async (_, _) => await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            StatusText.Text = "Loading accounts...";
            _accounts = await _httpClient.GetFromJsonAsync<List<AccountRow>>("api/User/management") ?? new();
            ApplyFilter();
            StatusText.Text = $"{_accounts.Count} account(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to load accounts.";
            MessageBox.Show(
                $"Unable to load accounts.\n\n{ex.Message}",
                "User Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _accounts
            : _accounts.Where(a =>
                a.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Role.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Status.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.UserId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        UsersGrid.ItemsSource = filtered;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAccountsAsync();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var fields = BuildAccountDialog("ADD ACCOUNT", "ADD");
        if (fields == null) return;

        try
        {
            SetBusy(fields.Value, true);

            var response = await _httpClient.PostAsJsonAsync(
                "api/User/management",
                new
                {
                    Username = fields.Value.Username.Text.Trim(),
                    Password = fields.Value.Password.Password,
                    Role = fields.Value.Role.SelectedItem?.ToString() ?? "Student"
                });

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    await ReadMessageAsync(response),
                    "User Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            fields.Value.Dialog.Close();
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "User Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(fields.Value, false);
        }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetAccount(sender) is not AccountRow account) return;

        string? username = ShowTextDialog(
            "Change Username",
            $"New username for {account.Username}",
            account.Username);

        if (string.IsNullOrWhiteSpace(username) ||
            username.Trim().Equals(account.Username, StringComparison.OrdinalIgnoreCase))
            return;

        await SendAsync(
            HttpMethod.Put,
            $"api/User/management/{account.UserId}/username",
            new { Username = username.Trim() },
            "Username updated successfully.");
    }

    private async void RoleButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetAccount(sender) is not AccountRow account) return;

        string? role = ShowRoleDialog(account.Role);
        if (role == null || role == account.Role || account.Status != "Active") return;

        await SendAsync(
            HttpMethod.Put,
            $"api/User/management/{account.UserId}/role",
            new { Role = role },
            "Role updated successfully.");
    }

    private async void PasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetAccount(sender) is not AccountRow account) return;

        string? password = ShowPasswordDialog(account.Username);
        if (password == null) return;

        await SendAsync(
            HttpMethod.Put,
            $"api/User/management/{account.UserId}/password",
            new { NewPassword = password },
            "Password reset successfully.");
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetAccount(sender) is not AccountRow account || account.Status != "Active") return;

        var result = MessageBox.Show(
            $"Deactivate account '{account.Username}'?\n\nThe account will no longer be able to log in, but its history will be preserved.",
            "Deactivate Account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await SendAsync(
            HttpMethod.Delete,
            $"api/User/management/{account.UserId}",
            null,
            "Account deactivated successfully.");
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetAccount(sender) is not AccountRow account || account.Status != "Inactive") return;

        var result = MessageBox.Show(
            $"Restore account '{account.Username}'?",
            "Restore Account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await SendAsync(
            HttpMethod.Post,
            $"api/User/management/{account.UserId}/restore",
            null,
            "Account restored successfully.");
    }

    private async Task SendAsync(
        HttpMethod method,
        string uri,
        object? body,
        string successMessage)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body != null)
                request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    await ReadMessageAsync(response),
                    "User Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = successMessage;
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "User Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();

        try
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? text;
        }
        catch
        {
        }

        return string.IsNullOrWhiteSpace(text)
            ? $"Request failed ({(int)response.StatusCode})."
            : $"Request failed ({(int)response.StatusCode}).\n\n{text}";
    }

    private static AccountRow? GetAccount(object sender) =>
        sender is Button { Tag: AccountRow account } ? account : null;

    private static string? ShowTextDialog(string title, string label, string value)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var box = new TextBox
        {
            Text = value,
            Height = 34,
            Padding = new Thickness(7)
        };
        panel.Children.Add(box);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        string? result = null;
        var cancel = new Button
        {
            Content = "CANCEL",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var save = new Button { Content = "SAVE", Width = 90, Height = 32 };

        cancel.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            result = box.Text;
            dialog.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog();
        return result;
    }

    private static string? ShowRoleDialog(string current)
    {
        var dialog = new Window
        {
            Title = "Change Role",
            Width = 380,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select new role",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var combo = new ComboBox { Height = 34 };
        combo.Items.Add("Student");
        combo.Items.Add("Teacher");
        combo.Items.Add("Admin");
        combo.SelectedItem = current;
        panel.Children.Add(combo);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        string? result = null;
        var cancel = new Button
        {
            Content = "CANCEL",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var save = new Button { Content = "SAVE", Width = 90, Height = 32 };

        cancel.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            result = combo.SelectedItem?.ToString();
            dialog.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog();
        return result;
    }

    private static string? ShowPasswordDialog(string username)
    {
        var dialog = new Window
        {
            Title = $"Reset Password - {username}",
            Width = 400,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = "New password",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var box = new PasswordBox
        {
            Height = 36,
            Padding = new Thickness(7)
        };
        panel.Children.Add(box);
        panel.Children.Add(new TextBlock
        {
            Text = "Minimum 6 characters",
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        string? result = null;
        var cancel = new Button
        {
            Content = "CANCEL",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var save = new Button { Content = "RESET", Width = 90, Height = 32 };

        cancel.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            if (box.Password.Length < 6)
            {
                MessageBox.Show(
                    "Password must be at least 6 characters.",
                    "User Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            result = box.Password;
            dialog.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog();
        return result;
    }

    private static (Window Dialog, TextBox Username, PasswordBox Password, ComboBox Role)? BuildAccountDialog(
        string title,
        string action)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 15)
        });

        var username = new TextBox
        {
            Height = 34,
            Padding = new Thickness(7)
        };
        panel.Children.Add(new TextBlock { Text = "Username", Foreground = Brushes.Black });
        panel.Children.Add(username);

        var password = new PasswordBox
        {
            Height = 34,
            Padding = new Thickness(7),
            Margin = new Thickness(0, 8, 0, 0)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Password",
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 10, 0, 0)
        });
        panel.Children.Add(password);

        var role = new ComboBox
        {
            Height = 34,
            Margin = new Thickness(0, 8, 0, 0)
        };
        role.Items.Add("Student");
        role.Items.Add("Teacher");
        role.Items.Add("Admin");
        role.SelectedIndex = 0;
        panel.Children.Add(new TextBlock
        {
            Text = "Role",
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 10, 0, 0)
        });
        panel.Children.Add(role);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancel = new Button
        {
            Content = "CANCEL",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var save = new Button { Content = action, Width = 90, Height = 32 };

        cancel.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(username.Text) || password.Password.Length < 6)
            {
                MessageBox.Show(
                    "Enter a username and a password with at least 6 characters.",
                    "User Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            dialog.DialogResult = true;
            dialog.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog();

        return dialog.DialogResult == true
            ? (dialog, username, password, role)
            : null;
    }

    private static void SetBusy(
        (Window Dialog, TextBox Username, PasswordBox Password, ComboBox Role) fields,
        bool busy)
    {
        fields.Username.IsEnabled = !busy;
        fields.Password.IsEnabled = !busy;
        fields.Role.IsEnabled = !busy;
    }

    private sealed class AccountRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
