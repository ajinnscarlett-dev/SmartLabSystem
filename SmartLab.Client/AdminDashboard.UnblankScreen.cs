using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - UNBLANK SCREEN
    // PHASE 3
    //
    // Adds an UNBLANK SCREEN button beside the existing
    // BLANK SCREEN control at runtime.
    //
    // No XAML replacement is required.
    //
    // Uses the existing PCCommandSimpleResponse model from
    // AdminDashboard.PcCommands.cs.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _unblankScreenHandlerRegistered =
                RegisterUnblankScreenHandler();

        private static bool
            RegisterUnblankScreenHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedForUnblankScreen));

            return true;
        }

        private static void
            AdminDashboardLoadedForUnblankScreen(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.AddUnblankScreenButtonIfMissing();
        }

        private void AddUnblankScreenButtonIfMissing()
        {
            if (FindName("UnblankScreenButton")
                is Button)
            {
                return;
            }

            Button? blankButton =
                FindName("BlankScreenButton")
                as Button;

            if (blankButton == null)
            {
                return;
            }

            if (blankButton.Parent is not Panel panel)
            {
                return;
            }

            int blankIndex =
                panel.Children.IndexOf(blankButton);

            if (blankIndex < 0)
            {
                return;
            }

            Button unblankButton =
                new Button
                {
                    Name =
                        "UnblankScreenButton",

                    Content =
                        "UNBLANK SCREEN",

                    Height =
                        30,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            5),

                    Style =
                        FindResource(
                            "ActionButton")
                        as Style,

                    IsEnabled =
                        true
                };

            unblankButton.Click +=
                UnblankScreenButton_Click;

            panel.Children.Insert(
                blankIndex + 1,
                unblankButton);
        }

        private async void UnblankScreenButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Unblank Screen",
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
                    "SmartLab - Unblank Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"Remove the blank screen from {_selectedPc.PcNumber}?",
                    "SmartLab - Unblank Screen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var response =
                    await _httpClient.PostAsJsonAsync(
                        $"api/PCCommand/{_selectedPc.PcId}/unblank-screen",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Unblank Screen",
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
                        ? "Unblank screen command queued."
                        : $"Unblank screen command queued successfully.\n\n" +
                          $"Command ID: {result.CommandId}",
                    "SmartLab - Unblank Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the unblank screen command.\n\n" +
                    ex.Message,
                    "SmartLab - Unblank Screen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
