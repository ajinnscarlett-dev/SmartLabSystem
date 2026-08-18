using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - LOG OFF USER
    // FIX 2
    //
    // The current AdminDashboard.xaml already contains a
    // LogoffUserButton.
    //
    // Therefore we MUST NOT create another runtime button.
    // Instead, we replace the existing placeholder click handler
    // with the real LOGOFF command handler.
    //
    // No XAML replacement is required.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _logoffHandlerRegistered =
                RegisterLogoffHandler();

        private static bool RegisterLogoffHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedForLogoff));

            return true;
        }

        private static void
            AdminDashboardLoadedForLogoff(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.BindExistingLogoffButton();
        }

        private void BindExistingLogoffButton()
        {
            Button? logoffButton =
                FindName("LogoffUserButton")
                as Button;

            if (logoffButton == null)
            {
                return;
            }

            // The XAML already has this button wired to the
            // old placeholder handler. Remove it and attach the
            // real command handler.
            logoffButton.Click -=
                UnsupportedQuickActionButton_Click;

            logoffButton.Click -=
                LogoffUserButton_Click;

            logoffButton.Click +=
                LogoffUserButton_Click;
        }

        private async void LogoffUserButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Log Off User",
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
                    "SmartLab - Log Off User",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"Log off the current user from {_selectedPc.PcNumber}?\n\n" +
                    "The student's Windows session will be signed out.",
                    "SmartLab - Log Off User",
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
                        $"api/PCCommand/{_selectedPc.PcId}/logoff",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Log Off User",
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
                        ? "Log off command queued."
                        : $"Log off command queued successfully.\n\n" +
                          $"Command ID: {result.CommandId}",
                    "SmartLab - Log Off User",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the log off command.\n\n" +
                    ex.Message,
                    "SmartLab - Log Off User",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
    