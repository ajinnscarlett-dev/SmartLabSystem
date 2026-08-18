using System;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class SmartLabWidget
    {
        // ==========================================================
        // WINDOWS LOCK / LOGOFF API
        // ==========================================================

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        private static extern bool LockWorkStation();

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        private static extern bool ExitWindowsEx(
            uint uFlags,
            uint dwReason);

        private const uint EwxLogoff = 0x00000000;
        private const uint EwxReboot = 0x00000002;

        // ==========================================================
        // BLANK SCREEN OVERLAY
        // ==========================================================

        private Window? _blankScreenWindow;
        private bool _blankScreenActive;
        private bool _closingBlankScreen;

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

        private static void SmartLabWidgetLoadedForCommands(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.InitializePcCommandPolling();
            }
        }

        private static void SmartLabWidgetUnloadedForCommands(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
            {
                widget.StopPcCommandPolling();
            }
        }

        private void InitializePcCommandPolling()
        {
            if (_pcCommandEventsInitialized)
            {
                return;
            }

            _pcCommandEventsInitialized = true;

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

        private void StopPcCommandPolling()
        {
            _pcCommandTimer?.Stop();

            CloseBlankScreen();

            _pcCommandEventsInitialized = false;
        }

        private async void PcCommandTimer_Tick(
            object? sender,
            EventArgs e)
        {
            await PollPcCommandAsync();
        }

        private async Task PollPcCommandAsync()
        {
            if (_pcCommandPollingRunning ||
                _isLoggingOut)
            {
                return;
            }

            _pcCommandPollingRunning = true;

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
                // Temporary network/server failures should
                // never terminate the Student session.
            }
            finally
            {
                _pcCommandPollingRunning =
                    false;
            }
        }

        private async Task HandlePcCommandAsync(
            PCCommandPendingResponse command)
        {
            bool success = false;
            string? result = null;
            bool logoffAfterAcknowledgement = false;
            bool restartAfterAcknowledgement = false;

            try
            {
                if (string.Equals(
                    command.CommandType,
                    "SEND_MESSAGE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ShowStudentCommandMessage(
                        command.Message?.Trim()
                        ?? string.Empty);

                    success = true;

                    result =
                        "Message displayed successfully.";
                }
                else if (string.Equals(
                    command.CommandType,
                    "LOCK_COMPUTER",
                    StringComparison.OrdinalIgnoreCase))
                {
                    bool locked =
                        LockWorkStation();

                    if (locked)
                    {
                        success = true;

                        result =
                            "Windows workstation locked.";
                    }
                    else
                    {
                        success = false;

                        result =
                            "Windows LockWorkStation failed. " +
                            "Win32 error: " +
                            Marshal.GetLastWin32Error();
                    }
                }
                else if (string.Equals(
                    command.CommandType,
                    "BLANK_SCREEN",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ShowBlankScreen();

                    success = true;

                    result =
                        "Student display blanked successfully.";
                }
                else if (string.Equals(
                    command.CommandType,
                    "UNBLANK_SCREEN",
                    StringComparison.OrdinalIgnoreCase))
                {
                    CloseBlankScreen();

                    success = true;

                    result =
                        "Student display unblanked successfully.";
                }
                else if (string.Equals(
                    command.CommandType,
                    "UNLOCK_REQUEST",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Safe behavior:
                    // do not attempt to bypass Windows credentials.
                    ShowUnlockRequestMessage();

                    success = true;

                    result =
                        "Unlock request displayed. " +
                        "Local Windows sign-in is required.";
                }

                else if (string.Equals(
                    command.CommandType,
                    "RESTART_COMPUTER",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Record the command result before Windows
                    // reboots, because the Student Client session
                    // will terminate immediately after restart begins.

                    success = true;

                    result =
                        "Windows restart request accepted. " +
                        "The computer will restart.";

                    restartAfterAcknowledgement = true;
                }
                else if (string.Equals(
                    command.CommandType,
                    "LOGOFF_USER",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Record the result before signing Windows out.
                    // This keeps the server-side Activity Log available
                    // even though the Student session will terminate.

                    success = true;

                    result =
                        "Windows logoff request accepted. " +
                        "The current user session will be signed out.";

                    logoffAfterAcknowledgement = true;
                }
                else
                {
                    success = false;

                    result =
                        "Unsupported command type.";
                }
            }
            catch (Exception ex)
            {
                success = false;
                result = ex.Message;
            }

            try
            {
                await _httpClient.PostAsJsonAsync(
                    $"api/PCCommand/{command.CommandId}/complete",
                    new
                    {
                        Success = success,
                        Result = result
                    });
            }
            catch
            {
                // Temporary acknowledgement failure should
                // not crash or close the Student client.
            }

            if (restartAfterAcknowledgement)
            {
                try
                {
                    await Task.Delay(250);

                    ExitWindowsEx(
                        EwxReboot,
                        0);
                }
                catch
                {
                    // The server result is already recorded.
                }
            }

            if (logoffAfterAcknowledgement)
            {
                try
                {
                    await Task.Delay(250);

                    ExitWindowsEx(
                        EwxLogoff,
                        0);
                }
                catch
                {
                    // If Windows rejects the logoff request, no
                    // additional UI action is attempted here.
                }
            }
        }

        private void ShowUnlockRequestMessage()
        {
            MessageBox.Show(
                this,
                "Your teacher requested that you unlock this computer.\n\n" +
                "Please sign in to Windows locally.",
                "SmartLab - Unlock Request",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowBlankScreen()
        {
            if (_blankScreenActive &&
                _blankScreenWindow != null)
            {
                _blankScreenWindow.Activate();
                return;
            }

            _blankScreenActive = true;
            _closingBlankScreen = false;

            Window overlay =
                new Window
                {
                    WindowStyle =
                        WindowStyle.None,

                    ResizeMode =
                        ResizeMode.NoResize,

                    ShowInTaskbar =
                        false,

                    ShowActivated =
                        true,

                    Topmost =
                        true,

                    Background =
                        Brushes.Black,

                    AllowsTransparency =
                        false,

                    WindowStartupLocation =
                        WindowStartupLocation.Manual,

                    Left =
                        SystemParameters.VirtualScreenLeft,

                    Top =
                        SystemParameters.VirtualScreenTop,

                    Width =
                        SystemParameters.VirtualScreenWidth,

                    Height =
                        SystemParameters.VirtualScreenHeight,

                    Title =
                        "SmartLab - Screen Blank"
                };

            overlay.Closing +=
                BlankScreenWindow_Closing;

            overlay.PreviewKeyDown +=
                BlankScreenWindow_PreviewKeyDown;

            overlay.PreviewMouseDown +=
                BlankScreenWindow_PreviewMouseDown;

            _blankScreenWindow =
                overlay;

            overlay.Show();
            overlay.Activate();
            overlay.Focus();
        }

        private void BlankScreenWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (!_closingBlankScreen)
            {
                e.Cancel = true;
            }
        }

        private void BlankScreenWindow_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            bool emergencyRecovery =
                Keyboard.Modifiers ==
                    (ModifierKeys.Control |
                     ModifierKeys.Shift |
                     ModifierKeys.Alt)
                &&
                e.Key == Key.R;

            if (emergencyRecovery)
            {
                CloseBlankScreen();
                e.Handled = true;
                return;
            }

            e.Handled = true;
        }

        private void BlankScreenWindow_PreviewMouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void CloseBlankScreen()
        {
            if (_blankScreenWindow == null)
            {
                _blankScreenActive = false;
                return;
            }

            try
            {
                _closingBlankScreen = true;
                _blankScreenWindow.Close();
            }
            catch
            {
                // Ignore cleanup errors.
            }
            finally
            {
                _blankScreenWindow = null;
                _blankScreenActive = false;
                _closingBlankScreen = false;
            }
        }

        private void ShowStudentCommandMessage(
            string message)
        {
            MessageBox.Show(
                this,
                message,
                "SmartLab - Message from Instructor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private sealed class PCCommandPendingResponse
        {
            public long CommandId { get; set; }
            public int PCId { get; set; }
            public string? CommandType { get; set; }
            public string? Message { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
