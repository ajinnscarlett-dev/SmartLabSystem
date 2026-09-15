using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLab.Client
{
    public partial class TeacherAssistanceWindow : Window
    {
        private readonly HttpClient _httpClient;

        public TeacherAssistanceWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient { BaseAddress = new Uri(SmartLabServerConfig.BaseUrl) };
            AuthSession.Apply(_httpClient);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadQueueAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadQueueAsync();

        private async Task LoadQueueAsync()
        {
            try
            {
                StatusText.Text = "Loading…";
                QueueGrid.ItemsSource = await _httpClient.GetFromJsonAsync<List<AssistanceRow>>("api/Assistance/queue") ?? new();
                StatusText.Text = $"Updated • {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Load failed";
                MessageBox.Show(ex.Message, "Assistance Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void Acknowledge_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync("Acknowledged");
        private async void Start_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync("In Progress");
        private async void Resolve_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync("Resolved");
        private async void CloseRequest_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync("Closed");

        private async Task UpdateSelectedAsync(string status)
        {
            if (QueueGrid.SelectedItem is not AssistanceRow request)
            {
                MessageBox.Show("Select a student request first.", "Assistance Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            object body = status is "Resolved" or "Closed"
                ? new { status, resolutionNotes = "Handled by teacher." }
                : new { status };

            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/Assistance/{request.AssistanceRequestId}/status", body);
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "Assistance Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadQueueAsync();
        }

        private void QueueGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (QueueGrid.SelectedItem is AssistanceRow request)
            {
                SelectedRequestText.Text = $"{request.StudentUsername} • {request.PCNumber} • {request.LaboratoryName} • {request.Status}";
                ResolutionText.Text = string.IsNullOrWhiteSpace(request.ResolutionNotes)
                    ? request.Description
                    : $"{request.Description}\n\nResolution: {request.ResolutionNotes}";
            }
            else
            {
                SelectedRequestText.Text = string.Empty;
                ResolutionText.Text = string.Empty;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class AssistanceRow
        {
            public long AssistanceRequestId { get; set; }
            public string? StudentUsername { get; set; }
            public string? PCNumber { get; set; }
            public string? LaboratoryName { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public string? ResolutionNotes { get; set; }
        }
    }
}
