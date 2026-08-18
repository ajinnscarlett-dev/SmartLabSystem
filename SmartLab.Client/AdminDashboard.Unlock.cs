using System;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - UNLOCK REQUEST
    // PHASE 3
    //
    // FIX:
    // The existing AdminDashboard.PcCommands.cs already contains
    // PCCommandSimpleResponse.
    //
    // This file therefore MUST NOT declare another class with the
    // same name. It reuses the existing response model from the
    // existing partial class.
    //
    // No XAML replacement is required.
    //
    // IMPORTANT:
    // This is NOT a Windows authentication bypass.
    // It sends an authorized UNLOCK REQUEST to the Student Client.
    // The student must sign in locally to Windows.
    // ==========================================================

    public partial class AdminDashboard
    {
        private static readonly bool
            _unlockRequestHandlerRegistered =
                RegisterUnlockRequestClassHandler();

        private static bool
            RegisterUnlockRequestClassHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedForUnlockRequest));

            return true;
        }

        private static void
            AdminDashboardLoadedForUnlockRequest(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.AddUnlockRequestButtonIfMissing();
        }

        private void AddUnlockRequestButtonIfMissing()
        {
            if (FindName("UnlockComputerButton")
                is Button)
            {
                return;
            }

            Button? lockButton =
                FindName("LockComputerButton")
                as Button;

            if (lockButton == null)
            {
                return;
            }

            if (lockButton.Parent is not Panel panel)
            {
                return;
            }

            int lockIndex =
                panel.Children.IndexOf(lockButton);

            if (lockIndex < 0)
            {
                return;
            }

            Button unlockButton =
                new Button
                {
                    Name =
                        "UnlockComputerButton",

                    Content =
                        "UNLOCK REQUEST",

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
                        as Style
                };

            unlockButton.Click +=
                UnlockComputerButton_Click;

            panel.Children.Insert(
                lockIndex + 1,
                unlockButton);
        }

        private async void UnlockComputerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab - Unlock Request",
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
                    "SmartLab - Unlock Request",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult confirm =
                MessageBox.Show(
                    $"Send an unlock request to {_selectedPc.PcNumber}?\n\n" +
                    "The student will be asked to sign in locally to Windows. " +
                    "SmartLab will not bypass Windows authentication.",
                    "SmartLab - Unlock Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var response =
                    await _httpClient.PostAsJsonAsync(
                        $"api/PCCommand/{_selectedPc.PcId}/unlock",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Unlock Request",
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
                        ? "Unlock request queued."
                        : $"Unlock request queued successfully.\n\n" +
                          $"Command ID: {result.CommandId}",
                    "SmartLab - Unlock Request",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to send the unlock request.\n\n" +
                    ex.Message,
                    "SmartLab - Unlock Request",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
