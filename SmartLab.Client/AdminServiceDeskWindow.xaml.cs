using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLab.Client
{
    public partial class AdminServiceDeskWindow : Window
    {
        private readonly HttpClient _httpClient;

        public AdminServiceDeskWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient { BaseAddress = new Uri(SmartLabServerConfig.BaseUrl) };
            AuthSession.Apply(_httpClient);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadTicketsAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadTicketsAsync();

        private async Task LoadTicketsAsync()
        {
            try
            {
                StatusText.Text = "Loading…";
                TicketGrid.ItemsSource = await _httpClient.GetFromJsonAsync<List<TicketRow>>("api/AdminServiceDesk") ?? new();
                StatusText.Text = $"Loaded • {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Load failed";
                MessageBox.Show(ex.Message, "Service Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(false);
        private async void Resolve_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(true);

        private async Task UpdateSelectedAsync(bool resolved)
        {
            if (TicketGrid.SelectedItem is not TicketRow ticket)
            {
                MessageBox.Show("Select a service desk ticket first.", "Service Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ResponseText.Text))
            {
                MessageBox.Show("Enter a response or resolution note first.", "Service Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var response = await _httpClient.PutAsJsonAsync($"api/AdminServiceDesk/{ticket.ServiceDeskTicketId}", new
            {
                status = resolved ? "Resolved" : "In Progress",
                message = ResponseText.Text.Trim(),
                assignedToUsername = string.IsNullOrWhiteSpace(AssignedToText.Text) ? null : AssignedToText.Text.Trim(),
                resolved
            });

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(body, "Service Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResponseText.Clear();
            await LoadTicketsAsync();
        }

        private void TicketGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TicketGrid.SelectedItem is TicketRow ticket)
            {
                AssignedToText.Text = ticket.AssignedToUsername ?? string.Empty;
                SelectedTicketText.Text = $"#{ticket.ServiceDeskTicketId} • {ticket.Status} • {ticket.Subject}";
            }
            else
            {
                SelectedTicketText.Text = string.Empty;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class TicketRow
        {
            public int ServiceDeskTicketId { get; set; }
            public int TeacherUserId { get; set; }
            public string TeacherUsername { get; set; } = string.Empty;
            public string? PCNumber { get; set; }
            public string? Location { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int? AssignedToUserId { get; set; }
            public string? AssignedToUsername { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? ResolvedAt { get; set; }
            public string? ResolutionNotes { get; set; }
        }
    }
}
