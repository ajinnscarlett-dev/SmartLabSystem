using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class SmartLabWidget
    {
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
                widget.InitializePcCommandPolling();
        }

        private static void SmartLabWidgetUnloadedForCommands(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is SmartLabWidget widget)
                widget.StopPcCommandPolling();
        }

        private void InitializePcCommandPolling()
        {
            if (_pcCommandEventsInitialized)
                return;

            _pcCommandEventsInitialized = true;

            _pcCommandTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _pcCommandTimer.Tick += PcCommandTimer_Tick;
            _pcCommandTimer.Start();

            _ = PollPcCommandAsync();
        }

        private void StopPcCommandPolling()
        {
            _pcCommandTimer?.Stop();
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
            if (_pcCommandPollingRunning || _isLoggingOut)
                return;

            _pcCommandPollingRunning = true;

            try
            {
                var response =
                    await _httpClient.GetAsync(
                        "api/PCCommand/pending");

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.NoContent)
                    return;

                if (!response.IsSuccessStatusCode)
                    return;

                PCCommandPendingResponse? command =
                    await response.Content
                        .ReadFromJsonAsync<
                            PCCommandPendingResponse>();

                if (command == null)
                    return;

                if (_lastProcessedPcCommandId ==
                    command.CommandId)
                    return;

                _lastProcessedPcCommandId =
                    command.CommandId;

                await HandlePcCommandAsync(command);
            }
            catch
            {
                // Temporary failure should not close the Student app.
            }
            finally
            {
                _pcCommandPollingRunning = false;
            }
        }

        private async Task HandlePcCommandAsync(
            PCCommandPendingResponse command)
        {
            bool success = false;
            string? result = null;

            try
            {
                if (string.Equals(
                    command.CommandType,
                    "SEND_MESSAGE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ShowStudentCommandMessage(
                        command.Message?.Trim() ?? string.Empty);

                    success = true;
                    result = "Message displayed successfully.";
                }
                else
                {
                    result = "Unsupported command type.";
                }
            }
            catch (Exception ex)
            {
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
                // The student stays signed in even if the acknowledgement
                // temporarily fails.
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
