using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLab.Client
{
    public partial class SmartLabWidget
    {
        private bool _operationalFeaturesInitialized;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_operationalFeaturesInitialized)
            {
                return;
            }

            _operationalFeaturesInitialized = true;
            _ = SyncHardwareInventoryAsync();
        }

        private async Task SyncHardwareInventoryAsync()
        {
            try
            {
                HardwareInventorySnapshot snapshot =
                    await Task.Run(HardwareInventoryCollector.Collect);

                var response = await _httpClient.PutAsJsonAsync(
                    $"api/HardwareInventory/{_pcId}/self",
                    new
                    {
                        cpu = snapshot.Cpu,
                        ram = snapshot.Ram,
                        storage = snapshot.Storage,
                        gpu = snapshot.Gpu,
                        operatingSystem = snapshot.OperatingSystem
                    });

                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                ConnectionStatusText.Text =
                    "Connected • hardware inventory synchronized";
            }
            catch
            {
                // Hardware inventory is best-effort and must never interrupt
                // the student's active SmartLab session.
            }
        }

        private async void NeedAssistanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                AssistanceDialog dialog = new AssistanceDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                NeedAssistanceButton.IsEnabled = false;

                var response = await _httpClient.PostAsJsonAsync(
                    "api/Assistance",
                    new
                    {
                        pcId = _pcId,
                        category = dialog.Category,
                        description = dialog.Description
                    });

                if (response.IsSuccessStatusCode)
                {
                    ConnectionStatusText.Text =
                        "Assistance request sent • Teacher/MIS notified";

                    MessageBox.Show(
                        "Your assistance request has been sent to the authorized teacher/MIS staff.",
                        "Need Assistance",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    string message = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(message)
                            ? "The assistance request could not be sent."
                            : message,
                        "Need Assistance",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to send the assistance request.\n\n{ex.Message}",
                    "Need Assistance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                NeedAssistanceButton.IsEnabled = true;
            }
        }
    }
}
