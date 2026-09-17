using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client;

public partial class AdminDashboard
{
    private async Task ShowPCIdentityDetailsAsync(PCInfo pc)
    {
        PCIdentityDetails? details = null;

        try
        {
            details = await _httpClient.GetFromJsonAsync<PCIdentityDetails>($"api/PC/{pc.PcId}");
        }
        catch
        {
            // Fall back to the fields already present in the dashboard's PC list.
        }

        details ??= new PCIdentityDetails
        {
            PcId = pc.PcId,
            PcNumber = pc.PcNumber,
            Status = pc.Status,
            Username = pc.Username,
            CurrentUserId = pc.CurrentUserId,
            LastSeen = pc.LastSeen,
            IsEnabled = pc.IsEnabled,
            MaintenanceReason = pc.MaintenanceReason,
            MaintenanceStarted = pc.MaintenanceStarted
        };

        var dialog = new Window
        {
            Title = $"{details.PcNumber} — Workstation Details",
            Width = 560,
            Height = 620,
            MinWidth = 520,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = (Brush)FindResource("SmartLabPageBackground"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "WORKSTATION IDENTITY",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("SmartLabText")
        });

        var subtitle = new TextBlock
        {
            Text = "Registered identity is anchored to the MAC address. IP address is current network metadata.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("SmartLabMuted"),
            Margin = new Thickness(0, 5, 0, 14)
        };
        Grid.SetRow(subtitle, 1);
        root.Children.Add(subtitle);

        var detailsPanel = new StackPanel();
        AddDetail(detailsPanel, "PC Number", DisplayOrDash(details.PcNumber));
        AddDetail(detailsPanel, "Registered MAC Address", DisplayOrDash(details.MACAddress));
        AddDetail(detailsPanel, "Current IP Address", DisplayOrDash(details.IPAddress));
        AddDetail(detailsPanel, "Last Seen", details.LastSeen.HasValue ? details.LastSeen.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Not seen");
        AddDetail(detailsPanel, "Status", DisplayOrDash(details.Status));
        AddDetail(detailsPanel, "Laboratory", DisplayOrDash(details.LaboratoryName ?? (details.LaboratoryId.HasValue ? $"Laboratory {details.LaboratoryId}" : null)));
        AddDetail(detailsPanel, "Current User", DisplayOrDash(details.Username ?? (details.CurrentUserId.HasValue ? $"User ID {details.CurrentUserId}" : null)));
        AddDetail(detailsPanel, "Enabled", details.IsEnabled ? "Yes" : "No");
        if (string.Equals(details.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            AddDetail(detailsPanel, "Maintenance Reason", DisplayOrDash(details.MaintenanceReason));
            AddDetail(detailsPanel, "Maintenance Started", details.MaintenanceStarted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not recorded");
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = detailsPanel
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        var close = new Button
        {
            Content = "CLOSE",
            Width = 90,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        close.Click += (_, _) => dialog.Close();
        Grid.SetRow(close, 3);
        root.Children.Add(close);

        dialog.Content = root;
        dialog.ShowDialog();
    }

    private void AddDetail(StackPanel panel, string label, string value)
    {
        panel.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("SmartLabBorder"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 10, 0, 10),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        Width = 180,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Foreground = (Brush)FindResource("SmartLabMuted"),
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = value,
                        Margin = new Thickness(190, 0, 0, 0),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)FindResource("SmartLabText")
                    }
                }
            }
        });
    }

    private static string DisplayOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private sealed class PCIdentityDetails
    {
        public int PcId { get; set; }
        public string PcNumber { get; set; } = string.Empty;
        public string? Status { get; set; }
        public int? CurrentUserId { get; set; }
        public string? Username { get; set; }
        public int? LaboratoryId { get; set; }
        public string? LaboratoryName { get; set; }
        public string? MACAddress { get; set; }
        public string? IPAddress { get; set; }
        public DateTime? LastSeen { get; set; }
        public bool IsEnabled { get; set; }
        public string? MaintenanceReason { get; set; }
        public DateTime? MaintenanceStarted { get; set; }
    }
}
