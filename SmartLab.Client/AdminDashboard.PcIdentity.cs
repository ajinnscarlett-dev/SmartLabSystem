using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client;

public partial class AdminDashboard
{
    private void ShowPCIdentityDetails(PCInfo pc)
    {
        var dialog = new Window
        {
            Title = $"{pc.PcNumber} — Workstation Details",
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

        var details = new StackPanel();
        AddDetail(details, "PC Number", pc.PcNumber);
        AddDetail(details, "Registered MAC Address", DisplayOrDash(pc.MACAddress));
        AddDetail(details, "Current IP Address", DisplayOrDash(pc.IPAddress));
        AddDetail(details, "Last Seen", pc.LastSeen.HasValue ? pc.LastSeen.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Not seen");
        AddDetail(details, "Status", DisplayOrDash(pc.Status));
        AddDetail(details, "Laboratory", DisplayOrDash(pc.LaboratoryName ?? (pc.LaboratoryId.HasValue ? $"Laboratory {pc.LaboratoryId}" : null)));
        AddDetail(details, "Current User", DisplayOrDash(pc.Username ?? (pc.CurrentUserId.HasValue ? $"User ID {pc.CurrentUserId}" : null)));
        AddDetail(details, "Enabled", pc.IsEnabled ? "Yes" : "No");
        if (string.Equals(pc.Status, "Maintenance", System.StringComparison.OrdinalIgnoreCase))
        {
            AddDetail(details, "Maintenance Reason", DisplayOrDash(pc.MaintenanceReason));
            AddDetail(details, "Maintenance Started", pc.MaintenanceStarted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not recorded");
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = details
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
}
