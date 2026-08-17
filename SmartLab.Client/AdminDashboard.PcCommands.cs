using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - PC COMMANDS
    // STEP 28 FINAL COMBINED FILE
    //
    // Contains BOTH:
    //   1. SEND MESSAGE
    //   2. LOCK COMPUTER
    //
    // This prevents the two command handlers from being split
    // across files and accidentally removed during replacement.
    // ==========================================================

    public partial class AdminDashboard
    {
        // ==========================================================
        // SEND MESSAGE
        // ==========================================================

        private async void SendMessageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Send Message",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            bool occupied =
                _selectedPc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                _selectedPc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);

            if (!occupied ||
                !_selectedPc.CurrentUserId.HasValue)
            {
                MessageBox.Show(
                    "A student must be logged in to this PC.",
                    "SmartLab - Send Message",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            Window dialog =
                new Window
                {
                    Title =
                        $"Send Message - {_selectedPc.PcNumber}",

                    Width = 520,
                    Height = 360,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    ResizeMode =
                        ResizeMode.NoResize,

                    Owner = this,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                7,
                                16,
                                25))
                };

            Grid root =
                new Grid
                {
                    Margin =
                        new Thickness(22)
                };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            TextBlock title =
                new TextBlock
                {
                    Text =
                        $"Send message to {_selectedPc.PcNumber}",

                    FontSize = 20,

                    FontWeight =
                        FontWeights.SemiBold,

                    Foreground =
                        Brushes.White,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            14)
                };

            Grid.SetRow(title, 0);
            root.Children.Add(title);

            TextBox messageBox =
                new TextBox
                {
                    AcceptsReturn = true,

                    TextWrapping =
                        TextWrapping.Wrap,

                    VerticalScrollBarVisibility =
                        ScrollBarVisibility.Auto,

                    MaxLength = 1000,

                    FontSize = 13,

                    Foreground =
                        Brushes.White,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                14,
                                28,
                                39)),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                42,
                                58,
                                69)),

                    Padding =
                        new Thickness(10)
                };

            Grid.SetRow(messageBox, 1);
            root.Children.Add(messageBox);

            StackPanel buttons =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0,
                            14,
                            0,
                            0)
                };

            Button cancelButton =
                new Button
                {
                    Content =
                        "CANCEL",

                    Width = 90,
                    Height = 34,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            8,
                            0)
                };

            Button sendButton =
                new Button
                {
                    Content =
                        "SEND",

                    Width = 90,
                    Height = 34,

                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                33,
                                93,
                                159)),

                    Foreground =
                        Brushes.White,

                    BorderThickness =
                        new Thickness(0)
                };

            cancelButton.Click +=
                (s, args) =>
                    dialog.Close();

            sendButton.Click += async (s, args) =>
            {
                string message =
                    messageBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(
                    message))
                {
                    MessageBox.Show(
                        "Enter a message first.",
                        "SmartLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    messageBox.Focus();
                    return;
                }

                sendButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                sendButton.Content = "SENDING...";

                try
                {
                    var request =
                        new PCCommandSendMessageRequest
                        {
                            Message =
                                message
                        };

                    var response =
                        await _httpClient.PostAsJsonAsync(
                            $"api/PCCommand/{_selectedPc.PcId}/send-message",
                            request);

                    string responseText =
                        await response.Content
                            .ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(
                            responseText,
                            "SmartLab - Send Message",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        sendButton.IsEnabled = true;
                        cancelButton.IsEnabled = true;
                        sendButton.Content = "SEND";

                        return;
                    }

                    PCCommandSendMessageResponse? result =
                        await response.Content
                            .ReadFromJsonAsync<
                                PCCommandSendMessageResponse>();

                    dialog.Close();

                    MessageBox.Show(
                        result == null
                            ? "Message command queued."
                            : $"Message queued successfully.\n\nCommand ID: {result.CommandId}",

                        "SmartLab - Send Message",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await LoadActivityLogs();
                }
                catch (Exception ex)
                {
                    sendButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                    sendButton.Content = "SEND";

                    MessageBox.Show(
                        "Unable to send the message.\n\n" +
                        ex.Message,

                        "SmartLab - Send Message",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(sendButton);

            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            dialog.Content =
                root;

            dialog.ShowDialog();
        }

        // ==========================================================
        // LOCK COMPUTER
        // ==========================================================

        private async void LockComputerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Lock Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            bool occupied =
                _selectedPc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                _selectedPc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);

            if (!occupied ||
                !_selectedPc.CurrentUserId.HasValue)
            {
                MessageBox.Show(
                    "A student must be logged in to this PC.",
                    "SmartLab - Lock Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"Lock {_selectedPc.PcNumber} now?\n\n" +
                    "The student's Windows session will be locked.",
                    "SmartLab - Lock Computer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var response =
                    await _httpClient.PostAsJsonAsync(
                        $"api/PCCommand/{_selectedPc.PcId}/lock",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Lock Computer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                PCCommandSimpleResponse? result =
                    await response.Content
                        .ReadFromJsonAsync<
                            PCCommandSimpleResponse>();

                MessageBox.Show(
                    result == null
                        ? "Lock command queued."
                        : $"Lock command queued successfully.\n\nCommand ID: {result.CommandId}",

                    "SmartLab - Lock Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the lock command.\n\n" +
                    ex.Message,

                    "SmartLab - Lock Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ==========================================================
        // RESPONSE MODELS
        // ==========================================================

        private sealed class PCCommandSendMessageRequest
        {
            public string Message { get; set; } =
                string.Empty;
        }

        private sealed class PCCommandSendMessageResponse
        {
            public long CommandId { get; set; }

            public int PCId { get; set; }

            public string? PcNumber { get; set; }

            public string? Status { get; set; }
        }

        private sealed class PCCommandSimpleResponse
        {
            public long CommandId { get; set; }

            public int PCId { get; set; }

            public string? PcNumber { get; set; }

            public string? CommandType { get; set; }

            public string? Status { get; set; }
        }
    }
}
