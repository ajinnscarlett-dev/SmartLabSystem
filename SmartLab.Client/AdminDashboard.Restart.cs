using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - RESTART COMPUTER
    // PHASE 3
    //
    // FIXED PATTERN:
    // The current AdminDashboard already contains the
    // RestartComputerButton in XAML.
    //
    // This file does NOT create another button.
    // It replaces the old placeholder click handler with the
    // real PC command handler.
    //
    // Reuses the existing PCCommandSimpleResponse model from
    // AdminDashboard.PcCommands.cs.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _restartHandlerRegistered =
                RegisterRestartHandler();

        private static bool RegisterRestartHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedForRestart));

            return true;
        }

        private static void
            AdminDashboardLoadedForRestart(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.BindExistingRestartButton();
        }

        private void BindExistingRestartButton()
        {
            Button? restartButton =
                FindName("RestartComputerButton")
                as Button;

            if (restartButton == null)
            {
                return;
            }

            restartButton.Click -=
                UnsupportedQuickActionButton_Click;

            restartButton.Click -=
                RestartComputerButton_Click;

            restartButton.Click +=
                RestartComputerButton_Click;
        }

        private async void RestartComputerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Restart Computer",
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
                    "SmartLab - Restart Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"Restart {_selectedPc.PcNumber}?\n\n" +
                    "The computer will restart and the current Windows session " +
                    "will be terminated.",
                    "SmartLab - Restart Computer",
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
                        $"api/PCCommand/{_selectedPc.PcId}/restart",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Restart Computer",
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
                        ? "Restart command queued."
                        : $"Restart command queued successfully.\n\n" +
                          $"Command ID: {result.CommandId}",
                    "SmartLab - Restart Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the restart command.\n\n" +
                    ex.Message,
                    "SmartLab - Restart Computer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
