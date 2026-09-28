using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartLab.Client
{
    public partial class AdminOperationsWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly string _adminUsername;
        private List<LabOption> _labs = new();
        private List<PcOption> _pcs = new();
        private List<UserOption> _users = new();

        public AdminOperationsWindow(string adminUsername)
        {
            InitializeComponent();

            _adminUsername = adminUsername;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(SmartLabServerConfig.BaseUrl)
            };
            AuthSession.Apply(_httpClient);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UsageFrom.SelectedDate = DateTime.Today.AddDays(-30);
            UsageTo.SelectedDate = DateTime.Today;
            AnnouncementRoleCombo.ItemsSource = new[] { "All", "Student", "Teacher", "Admin" };
            AnnouncementRoleCombo.SelectedIndex = 0;
            AssistanceStatusCombo.ItemsSource = new[] { "", "Open", "Acknowledged", "In Progress", "Resolved", "Closed" };
            AssistanceStatusCombo.SelectedIndex = 0;

            await LoadReferenceDataAsync();
            await RefreshAllAsync();
        }

        private async void RefreshAll_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

        private async Task RefreshAllAsync()
        {
            try
            {
                StatusText.Text = "Refreshing…";
                await LoadAnalyticsAsync();
                await LoadSystemHealthAsync();
                await LoadUsageAsync();
                await LoadMaintenanceAsync();
                await LoadInventoryAsync();
                await LoadAssistanceAsync();
                await LoadAnnouncementsAsync();
                await LoadArchiveAsync();
                StatusText.Text = $"Connected • {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Refresh failed";
                MessageBox.Show(ex.Message, "SmartLab", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task LoadReferenceDataAsync()
        {
            _labs = await GetJsonAsync<List<LabOption>>("api/Laboratory") ?? new();
            _pcs = await GetJsonAsync<List<PcOption>>("api/PC") ?? new();
            _users = await GetJsonAsync<List<UserOption>>("api/User") ?? new();

            var allLabs = new List<LabOption> { new() { LaboratoryId = 0, LabName = "All laboratories" } };
            allLabs.AddRange(_labs);
            UsageLabCombo.ItemsSource = allLabs;
            AssistanceLabCombo.ItemsSource = allLabs;
            UsageLabCombo.SelectedIndex = 0;
            AssistanceLabCombo.SelectedIndex = 0;

            var allPcs = new List<PcOption> { new() { PCId = 0, PCNumber = "All PCs" } };
            allPcs.AddRange(_pcs.OrderBy(p => p.PCNumber));
            UsagePcCombo.ItemsSource = allPcs;
            MaintenancePcCombo.ItemsSource = _pcs.OrderBy(p => p.PCNumber).ToList();
            UsagePcCombo.SelectedIndex = 0;

            var allUsers = new List<UserOption> { new() { UserId = 0, Username = "All students/users" } };
            allUsers.AddRange(_users.OrderBy(u => u.Username));
            UsageUserCombo.ItemsSource = allUsers;
            UsageUserCombo.SelectedIndex = 0;
        }

        private async Task LoadAnalyticsAsync()
        {
            var analytics = await GetJsonAsync<AnalyticsDto>("api/Analytics?from=" + Uri.EscapeDataString((UsageFrom.SelectedDate ?? DateTime.Today.AddDays(-30)).ToString("yyyy-MM-dd")) + "&to=" + Uri.EscapeDataString((UsageTo.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd")));
            if (analytics == null)
            {
                return;
            }

            TotalSessionsText.Text = analytics.TotalSessions.ToString();
            AvgSessionText.Text = FormatDuration(analytics.AverageSessionDurationSeconds);
            UtilizationText.Text = $"{analytics.CurrentUtilizationPercent:0.##}%";
            OfflineEventsText.Text = analytics.OfflineFrequency.ToString();
            MaintenanceEventsText.Text = analytics.MaintenanceFrequency.ToString();
            MostUsedPcText.Text = analytics.MostUsedPc?.PCNumber == null
                ? "PC —"
                : $"{analytics.MostUsedPc.PCNumber} • {analytics.MostUsedPc.Sessions} sessions";
            MostUsedLabText.Text = analytics.MostUsedLaboratory?.LaboratoryName == null
                ? "Laboratory —"
                : $"{analytics.MostUsedLaboratory.LaboratoryName} • {analytics.MostUsedLaboratory.Sessions} sessions";
            PeakHourText.Text = analytics.PeakUsageHour == null
                ? "Peak hour —"
                : $"Peak hour: {analytics.PeakUsageHour.Hour:00}:00 • {analytics.PeakUsageHour.Sessions} sessions";

            HealthAvailableText.Text = analytics.CurrentPcCounts?.Available.ToString() ?? "0";
            HealthOccupiedText.Text = analytics.CurrentPcCounts?.Occupied.ToString() ?? "0";
            HealthOfflineText.Text = analytics.CurrentPcCounts?.Offline.ToString() ?? "0";
            HealthMaintenanceText.Text = analytics.CurrentPcCounts?.Maintenance.ToString() ?? "0";
        }

        private async Task LoadSystemHealthAsync()
        {
            try
            {
                var health = await GetJsonAsync<HealthDto>("api/Health");
                if (health == null)
                {
                    SystemHealthText.Text = "Health endpoint returned no data.";
                    return;
                }

                bool databaseOk = health.DatabaseConnected;
                SystemHealthText.Text = databaseOk
                    ? "SmartLab Server + database reachable"
                    : "SmartLab Server reachable, database connection failed";
                SystemHealthDetailText.Text = $"PCs: {health.TotalPCs} total • Available {health.Available} • Occupied {health.Occupied} • Maintenance {health.Maintenance} • Offline {health.Offline}";
            }
            catch (Exception ex)
            {
                SystemHealthText.Text = "Unable to read system health.";
                SystemHealthDetailText.Text = ex.Message;
            }
        }

        private async Task LoadUsageAsync()
        {
            try
            {
                string query = BuildUsageQuery();
                var rows = await GetJsonAsync<List<UsageRow>>("api/UsageHistory" + query) ?? new();
                UsageGrid.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                UsageGrid.ItemsSource = new List<UsageRow>();
                StatusText.Text = "Usage load failed";
                Console.WriteLine(ex);
            }
        }

        private string BuildUsageQuery()
        {
            var parts = new List<string>();
            if (UsageFrom.SelectedDate.HasValue) parts.Add("from=" + Uri.EscapeDataString(UsageFrom.SelectedDate.Value.ToString("yyyy-MM-dd")));
            if (UsageTo.SelectedDate.HasValue) parts.Add("to=" + Uri.EscapeDataString(UsageTo.SelectedDate.Value.ToString("yyyy-MM-dd")));
            if (UsageLabCombo.SelectedItem is LabOption lab && lab.LaboratoryId > 0) parts.Add($"laboratoryId={lab.LaboratoryId}");
            if (UsagePcCombo.SelectedItem is PcOption pc && pc.PCId > 0) parts.Add($"pcId={pc.PCId}");
            if (UsageUserCombo.SelectedItem is UserOption user && user.UserId > 0) parts.Add($"userId={user.UserId}");
            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }

        private async Task LoadMaintenanceAsync()
        {
            var rows = await GetJsonAsync<List<MaintenanceRow>>("api/Maintenance") ?? new();
            MaintenanceGrid.ItemsSource = rows;
        }

        private async Task LoadInventoryAsync()
        {
            var rows = await GetJsonAsync<List<InventoryRow>>("api/HardwareInventory") ?? new();
            InventoryGrid.ItemsSource = rows;
        }

        private async Task LoadAssistanceAsync()
        {
            var parts = new List<string>();
            if (AssistanceLabCombo.SelectedItem is LabOption lab && lab.LaboratoryId > 0)
                parts.Add($"laboratoryId={lab.LaboratoryId}");
            if (AssistanceStatusCombo.SelectedItem is string status && !string.IsNullOrWhiteSpace(status))
                parts.Add("status=" + Uri.EscapeDataString(status));

            string query = parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
            var rows = await GetJsonAsync<List<AssistanceRow>>("api/Assistance/queue" + query) ?? new();
            AssistanceGrid.ItemsSource = rows;
        }

        private async Task LoadAnnouncementsAsync()
        {
            var rows = await GetJsonAsync<List<AnnouncementRow>>("api/Announcement/all") ?? new();
            AnnouncementGrid.ItemsSource = rows;
        }

        private async Task LoadActivityLogsAsync()
        {
            try
            {
                var rows = await GetJsonAsync<List<ActivityLogRow>>("api/ActivityLog") ?? new();
                ActivityLogGrid.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                ActivityLogGrid.ItemsSource = new List<ActivityLogRow>();
                StatusText.Text = "Activity log load failed";
                Console.WriteLine(ex);
            }
        }

        private async Task LoadArchiveAsync()
        {
            int ageDays = ParseArchiveAge();
            var plan = await GetJsonAsync<ArchivePlan>($"api/Archive/plan?ageDays={ageDays}");
            if (plan == null)
            {
                ArchiveSummaryText.Text = "Unable to load archive state.";
                return;
            }

            ArchiveSummaryText.Text = $"Eligible records older than {plan.AgeDays} days: {plan.TotalEligible:N0}";
            ArchiveDetailText.Text =
                $"Usage sessions: {plan.UsageHistoryCount:N0} • Maintenance: {plan.MaintenanceCount:N0} • Service Desk: {plan.ServiceDeskCount:N0} • Activity: {plan.ActivityLogCount:N0}. " +
                "The current archive operation is export-first and does not delete active or historical rows automatically.";
        }

        private int ParseArchiveAge()
        {
            return int.TryParse(ArchiveAgeText.Text, out int age) ? Math.Clamp(age, 30, 3650) : 60;
        }

        private async void LoadUsage_Click(object sender, RoutedEventArgs e) => await LoadUsageAsync();
        private async void LoadMaintenance_Click(object sender, RoutedEventArgs e) => await LoadMaintenanceAsync();
        private async void LoadInventory_Click(object sender, RoutedEventArgs e) => await LoadInventoryAsync();
        private async void LoadAssistance_Click(object sender, RoutedEventArgs e) => await LoadAssistanceAsync();
        private async void LoadAnnouncements_Click(object sender, RoutedEventArgs e) => await LoadAnnouncementsAsync();
        private async void LoadArchive_Click(object sender, RoutedEventArgs e) => await LoadArchiveAsync();

        private async void StartMaintenance_Click(object sender, RoutedEventArgs e)
        {
            if (MaintenancePcCombo.SelectedItem is not PcOption pc)
            {
                MessageBox.Show("Select a PC first.", "Maintenance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string reason = string.IsNullOrWhiteSpace(MaintenanceReasonText.Text)
                ? "Maintenance required"
                : MaintenanceReasonText.Text.Trim();

            var response = await _httpClient.PutAsJsonAsync($"api/PC/{pc.PCId}/maintenance", new { reason });
            await ShowResponseAsync(response, "Start maintenance");
            await LoadReferenceDataAsync();
            await LoadMaintenanceAsync();
            await LoadAnalyticsAsync();
        }

        private async void ClearMaintenance_Click(object sender, RoutedEventArgs e)
        {
            if (MaintenancePcCombo.SelectedItem is not PcOption pc)
            {
                MessageBox.Show("Select a PC first.", "Maintenance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var response = await _httpClient.PutAsync($"api/PC/{pc.PCId}/maintenance/clear", null);
            await ShowResponseAsync(response, "Clear maintenance");
            await LoadReferenceDataAsync();
            await LoadMaintenanceAsync();
            await LoadAnalyticsAsync();
        }

        private async Task ChangeAssistanceStatusAsync(string status)
        {
            if (AssistanceGrid.SelectedItem is not AssistanceRow row)
            {
                MessageBox.Show("Select an assistance request first.", "Assistance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            object request = status is "Resolved" or "Closed"
                ? new { status, resolutionNotes = "Handled by Admin/MIS." }
                : new { status };

            var response = await _httpClient.PutAsJsonAsync($"api/Assistance/{row.AssistanceRequestId}/status", request);
            await ShowResponseAsync(response, status);
            await LoadAssistanceAsync();
        }

        private async void AcknowledgeAssistance_Click(object sender, RoutedEventArgs e) => await ChangeAssistanceStatusAsync("Acknowledged");
        private async void StartAssistance_Click(object sender, RoutedEventArgs e) => await ChangeAssistanceStatusAsync("In Progress");
        private async void ResolveAssistance_Click(object sender, RoutedEventArgs e) => await ChangeAssistanceStatusAsync("Resolved");
        private async void CloseAssistance_Click(object sender, RoutedEventArgs e) => await ChangeAssistanceStatusAsync("Closed");

        private async void CreateAnnouncement_Click(object sender, RoutedEventArgs e)
        {
            string title = AnnouncementTitleText.Text.Trim();
            string message = AnnouncementMessageText.Text.Trim();
            string role = AnnouncementRoleCombo.SelectedItem?.ToString() ?? "All";

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Title and message are required.", "Announcements", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime? expiry = AnnouncementExpiry.SelectedDate?.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            var response = await _httpClient.PostAsJsonAsync("api/Announcement", new
            {
                title,
                message,
                expiresAt = expiry,
                targetRole = role,
                postedByUserId = 0
            });

            await ShowResponseAsync(response, "Create announcement");
            if (response.IsSuccessStatusCode)
            {
                AnnouncementTitleText.Clear();
                AnnouncementMessageText.Clear();
                AnnouncementExpiry.SelectedDate = null;
                await LoadAnnouncementsAsync();
            }
        }

        private async void DeactivateAnnouncement_Click(object sender, RoutedEventArgs e)
        {
            if (AnnouncementGrid.SelectedItem is not AnnouncementRow row)
            {
                MessageBox.Show("Select an announcement first.", "Announcements", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var response = await _httpClient.PutAsync($"api/Announcement/{row.AnnouncementId}/deactivate", null);
            await ShowResponseAsync(response, "Deactivate announcement");
            await LoadAnnouncementsAsync();
        }

        private async void ExportUsage_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync("api/Reports/usage.csv" + BuildUsageQuery(), "smartlab-usage.csv");
        private async void ExportMaintenance_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync("api/Reports/maintenance.csv" + BuildDateQuery(), "smartlab-maintenance.csv");
        private async void ExportInventory_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync("api/Reports/inventory.csv", "smartlab-inventory.csv");
        private async void ExportServiceDesk_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync("api/Reports/service-desk.csv", "smartlab-service-desk.csv");
        private async void ExportActivity_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync("api/Reports/activity.csv" + BuildDateQuery(), "smartlab-activity.csv");
        private async void ExportArchiveUsage_Click(object sender, RoutedEventArgs e) => await ExportEndpointAsync($"api/Archive/usage.csv?ageDays={ParseArchiveAge()}", "smartlab-archive-usage.csv");

        private string BuildDateQuery()
        {
            var parts = new List<string>();
            if (UsageFrom.SelectedDate.HasValue) parts.Add("from=" + Uri.EscapeDataString(UsageFrom.SelectedDate.Value.ToString("yyyy-MM-dd")));
            if (UsageTo.SelectedDate.HasValue) parts.Add("to=" + Uri.EscapeDataString(UsageTo.SelectedDate.Value.ToString("yyyy-MM-dd")));
            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }

        private async Task ExportEndpointAsync(string endpoint, string defaultName)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                {
                    await ShowResponseAsync(response, "Export");
                    return;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                var dialog = new SaveFileDialog
                {
                    FileName = defaultName,
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog(this) == true)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, bytes);
                    StatusText.Text = $"Exported • {Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class ActivityLogRow
        {
            public DateTime CreatedAt { get; set; }
            public string? Username { get; set; }
            public string? Role { get; set; }
            public string? Action { get; set; }
            public string? PcNumber { get; set; }
            public string? Details { get; set; }
        }


        private async Task<T?> GetJsonAsync<T>(string endpoint)
        {
            using var response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        }

        private async Task ShowResponseAsync(HttpResponseMessage response, string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string text = await response.Content.ReadAsStringAsync();
            MessageBox.Show(string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase : text,
                operation, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "0m";
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"{Math.Max(1, duration.Minutes)}m";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class LabOption
        {
            public int LaboratoryId { get; set; }
            public string LabName { get; set; } = string.Empty;
            public int PCCount { get; set; }
            public override string ToString() => LaboratoryId == 0 ? LabName : $"{LabName} ({PCCount})";
        }

        private sealed class PcOption
        {
            public int PCId { get; set; }
            public string PCNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public override string ToString() => PCId == 0 ? PCNumber : $"{PCNumber} • {Status}";
        }

        private sealed class UserOption
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public override string ToString() => Username;
        }

        private sealed class UsageRow
        {
            public long SessionId { get; set; }
            public int PCId { get; set; }
            public string? PCNumber { get; set; }
            public int UserId { get; set; }
            public string? Username { get; set; }
            public int? LaboratoryId { get; set; }
            public string? LaboratoryName { get; set; }
            public DateTime LoginTime { get; set; }
            public DateTime? LogoutTime { get; set; }
            public int? DurationSeconds { get; set; }
            public string EndReason { get; set; } = string.Empty;
        }

        private sealed class MaintenanceRow
        {
            public long MaintenanceRecordId { get; set; }
            public int PCId { get; set; }
            public string? PCNumber { get; set; }
            public string Reason { get; set; } = string.Empty;
            public DateTime StartedAt { get; set; }
            public DateTime? EndedAt { get; set; }
            public int? TechnicianUserId { get; set; }
            public string? Technician { get; set; }
            public string? Notes { get; set; }
        }

        private sealed class InventoryRow
        {
            public int HardwareInventoryId { get; set; }
            public int PCId { get; set; }
            public string? PCNumber { get; set; }
            public int? LaboratoryId { get; set; }
            public string? LaboratoryName { get; set; }
            public string? Cpu { get; set; }
            public string? Ram { get; set; }
            public string? Storage { get; set; }
            public string? Gpu { get; set; }
            public string? OperatingSystem { get; set; }
            public string? MACAddress { get; set; }
            public string? IPAddress { get; set; }
            public DateTime LastAuditedAt { get; set; }
        }

        private sealed class AssistanceRow
        {
            public long AssistanceRequestId { get; set; }
            public int StudentUserId { get; set; }
            public string? StudentUsername { get; set; }
            public int PCId { get; set; }
            public string? PCNumber { get; set; }
            public int? LaboratoryId { get; set; }
            public string? LaboratoryName { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? AcknowledgedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? ResolvedAt { get; set; }
            public DateTime? ClosedAt { get; set; }
            public string? ResolutionNotes { get; set; }
        }

        private sealed class AnnouncementRow
        {
            public int AnnouncementId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public bool IsActive { get; set; }
            public int? PostedByUserId { get; set; }
            public string TargetRole { get; set; } = "All";
        }

        private sealed class AnalyticsDto
        {
            public int TotalSessions { get; set; }
            public double AverageSessionDurationSeconds { get; set; }
            public MetricPc? MostUsedPc { get; set; }
            public MetricLab? MostUsedLaboratory { get; set; }
            public MetricHour? PeakUsageHour { get; set; }
            public int OfflineFrequency { get; set; }
            public int MaintenanceFrequency { get; set; }
            public int ServiceDeskVolume { get; set; }
            public double CurrentUtilizationPercent { get; set; }
            public PcCounts? CurrentPcCounts { get; set; }
        }

        private sealed class MetricPc { public int PCId { get; set; } public string? PCNumber { get; set; } public int Sessions { get; set; } }
        private sealed class MetricLab { public int? LaboratoryId { get; set; } public string? LaboratoryName { get; set; } public int Sessions { get; set; } }
        private sealed class MetricHour { public int Hour { get; set; } public int Sessions { get; set; } }
        private sealed class PcCounts { public int Available { get; set; } public int Occupied { get; set; } public int Offline { get; set; } public int Maintenance { get; set; } public int Total { get; set; } }
        private sealed class HealthDto { public bool DatabaseConnected { get; set; } public int TotalPCs { get; set; } public int Available { get; set; } public int Occupied { get; set; } public int Maintenance { get; set; } public int Offline { get; set; } }
        private sealed class ArchivePlan
        {
            public int AgeDays { get; set; }
            public int UsageHistoryCount { get; set; }
            public int MaintenanceCount { get; set; }
            public int ServiceDeskCount { get; set; }
            public int ActivityLogCount { get; set; }
            public int TotalEligible { get; set; }
        }
    }
}
