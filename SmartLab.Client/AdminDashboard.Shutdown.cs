using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - SHUTDOWN COMPUTER
    // PHASE 3
    //
    // Uses the EXISTING ShutdownComputerButton from XAML.
    // Does not create a second button.
    //
    // Reuses the existing PCCommandSimpleResponse model.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _shutdownHandlerRegistered =
                RegisterShutdownHandler();

        private static bool RegisterShutdownHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedForShutdown));

            return true;
        }

        private static void
            AdminDashboardLoadedForShutdown(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.BindExistingShutdownButton();
        }

        private void BindExistingShutdownButton()
        {
            Button? shutdownButton =
                FindName("ShutdownComputerButton")
                as Button;

            if (shutdownButton == null)
            {
                return;
            }

            shutdownButton.Click -=
                UnsupportedQuickActionButton_Click;

            shutdownButton.Click -=
                ShutdownComputerButton_Click;

            shutdownButton.Click +=
                ShutdownComputerButton_Click;
        }

        private async void ShutdownComputerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Shutdown Computer",
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
                    "SmartLab - Shutdown Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"SHUT DOWN {_selectedPc.PcNumber}?\n\n" +
                    "The computer will completely power off and the current " +
                    "Windows session will end.\n\n" +
                    "Save all work first.",
                    "SmartLab - Shutdown Computer",
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
                        $"api/PCCommand/{_selectedPc.PcId}/shutdown",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Shutdown Computer",
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
                        ? "Shutdown command queued."
                        : $"Shutdown command queued successfully.\n\n" +
                          $"Command ID: {result.CommandId}",
                    "SmartLab - Shutdown Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the shutdown command.\n\n" +
                    ex.Message,
                    "SmartLab - Shutdown Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
