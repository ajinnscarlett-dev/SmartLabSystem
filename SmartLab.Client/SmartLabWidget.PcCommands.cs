using System;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // STUDENT PC COMMAND RECEIVER
    // STEP 28 - COMPLETE VERSION
    //
    // IMPORTANT:
    // This file includes BOTH the Step 27 SEND_MESSAGE command
    // receiver and the Step 28 LOCK_COMPUTER command.
    //
    // It also restores the missing:
    //   PCCommandPendingResponse
    //   ShowStudentCommandMessage()
    //
    // Keep the existing SmartLabWidget.xaml.cs unchanged.
    // ==========================================================

    public partial class SmartLabWidget
    {
        // ==========================================================
        // WINDOWS LOCK API
        // ==========================================================

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        private static extern bool LockWorkStation();

        // ==========================================================
        // COMMAND POLLING
        // ==========================================================

        private DispatcherTimer? _pcCommandTimer;

        private bool _pcCommandPollingRunning;

        private long _lastProcessedPcCommandId;

        private bool _pcCommandEventsInitialized;

        static SmartLabWidget()
        {
            EventManager.RegisterClassHandler(
                typeof(SmartLabWidget),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    SmartLabWidgetLoadedForCommands));

            EventManager.RegisterClassHandler(
                typeof(SmartLabWidget),
                FrameworkElement.UnloadedEvent,
                new RoutedEventHandler(
                    SmartLabWidgetUnloadedForCommands));
        }

        private static void
            SmartLabWidgetLoadedForCommands(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.InitializePcCommandPolling();
            }
        }

        private static void
            SmartLabWidgetUnloadedForCommands(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.StopPcCommandPolling();
            }
        }

        // ==========================================================
        // INITIALIZE
        // ==========================================================

        private void InitializePcCommandPolling()
        {
            if (_pcCommandEventsInitialized)
            {
                return;
            }

            _pcCommandEventsInitialized =
                true;

            _pcCommandTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(1)
                };

            _pcCommandTimer.Tick +=
                PcCommandTimer_Tick;

            _pcCommandTimer.Start();

            _ = PollPcCommandAsync();
        }

        // ==========================================================
        // STOP
        // ==========================================================

        private void StopPcCommandPolling()
        {
            _pcCommandTimer?.Stop();

            _pcCommandEventsInitialized =
                false;
        }

        // ==========================================================
        // TIMER
        // ==========================================================

        private async void
            PcCommandTimer_Tick(
                object? sender,
                EventArgs e)
        {
            await PollPcCommandAsync();
        }

        // ==========================================================
        // POLL PENDING COMMAND
        // ==========================================================

        private async Task
            PollPcCommandAsync()
        {
            if (_pcCommandPollingRunning ||
                _isLoggingOut)
            {
                return;
            }

            _pcCommandPollingRunning =
                true;

            try
            {
                var response =
                    await _httpClient.GetAsync(
                        "api/PCCommand/pending");

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.NoContent)
                {
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                PCCommandPendingResponse? command =
                    await response.Content
                        .ReadFromJsonAsync<
                            PCCommandPendingResponse>();

                if (command == null)
                {
                    return;
                }

                // Prevent processing the exact same command twice
                // while the acknowledgement is being completed.
                if (_lastProcessedPcCommandId ==
                    command.CommandId)
                {
                    return;
                }

                _lastProcessedPcCommandId =
                    command.CommandId;

                await HandlePcCommandAsync(
                    command);
            }
            catch
            {
                // Temporary network/server failures should never
                // terminate the Student session.
            }
            finally
            {
                _pcCommandPollingRunning =
                    false;
            }
        }

        // ==========================================================
        // HANDLE COMMAND
        // ==========================================================

        private async Task
            HandlePcCommandAsync(
                PCCommandPendingResponse command)
        {
            bool success =
                false;

            string? result =
                null;

            try
            {
                // --------------------------------------------------
                // SEND MESSAGE
                // --------------------------------------------------

                if (string.Equals(
                    command.CommandType,
                    "SEND_MESSAGE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ShowStudentCommandMessage(
                        command.Message?.Trim()
                        ?? string.Empty);

                    success =
                        true;

                    result =
                        "Message displayed successfully.";
                }

                // --------------------------------------------------
                // LOCK COMPUTER
                // --------------------------------------------------

                else if (
                    string.Equals(
                        command.CommandType,
                        "LOCK_COMPUTER",
                        StringComparison.OrdinalIgnoreCase))
                {
                    bool locked =
                        LockWorkStation();

                    if (locked)
                    {
                        success =
                            true;

                        result =
                            "Windows workstation locked.";
                    }
                    else
                    {
                        success =
                            false;

                        result =
                            $"Windows LockWorkStation failed. " +
                            $"Win32 error: " +
                            $"{Marshal.GetLastWin32Error()}";
                    }
                }

                // --------------------------------------------------
                // UNKNOWN COMMAND
                // --------------------------------------------------

                else
                {
                    success =
                        false;

                    result =
                        "Unsupported command type.";
                }
            }
            catch (Exception ex)
            {
                success =
                    false;

                result =
                    ex.Message;
            }

            // ------------------------------------------------------
            // ACKNOWLEDGE RESULT
            // ------------------------------------------------------

            try
            {
                await _httpClient.PostAsJsonAsync(
                    $"api/PCCommand/{command.CommandId}/complete",
                    new
                    {
                        Success =
                            success,

                        Result =
                            result
                    });
            }
            catch
            {
                // Temporary acknowledgement failure should not
                // crash or close the Student client.
            }
        }

        // ==========================================================
        // STUDENT MESSAGE
        // ==========================================================

        private void
            ShowStudentCommandMessage(
                string message)
        {
            MessageBox.Show(
                this,
                message,
                "SmartLab - Message from Instructor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ==========================================================
        // COMMAND DTO
        // ==========================================================

        private sealed class
            PCCommandPendingResponse
        {
            public long CommandId { get; set; }

            public int PCId { get; set; }

            public string? CommandType { get; set; }

            public string? Message { get; set; }

            public DateTime CreatedAt { get; set; }
        }
    }
}
