using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client
{
    public partial class AdminOperationsWindow
    {
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += AddAdvancedOperationsButtons;
        }

        private void AddAdvancedOperationsButtons(object? sender, RoutedEventArgs e)
        {
            Loaded -= AddAdvancedOperationsButtons;

            TabItem? exportsTab = FindTab("EXPORTS");
            if (exportsTab != null)
            {
                WrapPanel? panel = FindVisualChildren<WrapPanel>(exportsTab).FirstOrDefault();
                if (panel != null)
                {
                    foreach (string report in new[] { "usage", "maintenance", "inventory", "service-desk", "activity" })
                    {
                        string title = report.Replace("-", " ").ToUpperInvariant();
                        foreach (string format in new[] { "xls", "pdf" })
                        {
                            var button = new Button
                            {
                                Content = $"{title} {format.ToUpperInvariant()}",
                                Margin = new Thickness(0, 0, 7, 7),
                                Padding = new Thickness(11, 7, 11, 7),
                                Background = new SolidColorBrush(Color.FromRgb(20, 35, 45)),
                                Foreground = Brushes.White
                            };
                            string endpoint = $"api/ReportFiles/{report}.{format}";
                            string extension = format == "xls" ? "xls" : "pdf";
                            button.Click += async (_, _) => await ExportAdvancedAsync(endpoint, $"smartlab-{report}.{extension}", extension);
                            panel.Children.Add(button);
                        }
                    }
                }
            }

            TabItem? archiveTab = FindTab("ARCHIVE");
            if (archiveTab != null)
            {
                StackPanel? archivePanel = FindVisualChildren<StackPanel>(archiveTab).FirstOrDefault(p => p.Children.OfType<Button>().Any(b => string.Equals(b.Content?.ToString(), "CHECK ARCHIVE STATE", StringComparison.OrdinalIgnoreCase)));
                if (archivePanel != null)
                {
                    var archiveButton = new Button
                    {
                        Content = "RUN ARCHIVE BUNDLE",
                        Margin = new Thickness(7, 0, 7, 0),
                        Padding = new Thickness(11, 7, 11, 7),
                        Background = new SolidColorBrush(Color.FromRgb(20, 35, 45)),
                        Foreground = Brushes.White
                    };
                    archiveButton.Click += async (_, _) => await RunArchiveBundleAsync();
                    archivePanel.Children.Add(archiveButton);
                }
            }

            TabItem? announcementTab = FindTab("ANNOUNCEMENTS");
            if (announcementTab != null)
            {
                StackPanel? announcementButtons = FindVisualChildren<StackPanel>(announcementTab).FirstOrDefault(p => p.Children.OfType<Button>().Any(b => string.Equals(b.Content?.ToString(), "CREATE", StringComparison.OrdinalIgnoreCase)));
                if (announcementButtons != null)
                {
                    var editButton = new Button
                    {
                        Content = "EDIT SELECTED",
                        Background = new SolidColorBrush(Color.FromRgb(20, 35, 45)),
                        Foreground = Brushes.White,
                        Padding = new Thickness(11, 7, 11, 7),
                        Margin = new Thickness(0, 0, 7, 0)
                    };
                    editButton.Click += EditAnnouncement_Click;
                    announcementButtons.Children.Add(editButton);
                }
            }
        }

        private TabItem? FindTab(string header)
        {
            return FindVisualChildren<TabItem>(this).FirstOrDefault(tab => string.Equals(tab.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));
        }

        private async void EditAnnouncement_Click(object sender, RoutedEventArgs e)
        {
            if (AnnouncementGrid.SelectedItem is not AnnouncementRow selected)
            {
                MessageBox.Show("Select an announcement first.", "Announcements", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string title = AnnouncementTitleText.Text.Trim();
            string message = AnnouncementMessageText.Text.Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Title and message are required.", "Announcements", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/Announcement/{selected.AnnouncementId}", new
                {
                    title,
                    message,
                    expiresAt = AnnouncementExpiry.SelectedDate?.Date.AddHours(23).AddMinutes(59).AddSeconds(59),
                    targetRole = AnnouncementRoleCombo.SelectedItem?.ToString() ?? "All"
                });

                await ShowResponseAsync(response, "Edit announcement");
                if (response.IsSuccessStatusCode)
                    await LoadAnnouncementsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Announcements", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task RunArchiveBundleAsync()
        {
            int ageDays = ParseArchiveAge();
            try
            {
                using HttpResponseMessage response = await _httpClient.PostAsync($"api/Archive/run?ageDays={ageDays}", null);
                if (!response.IsSuccessStatusCode)
                {
                    await ShowResponseAsync(response, "Archive");
                    return;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"smartlab-archive-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
                    Filter = "ZIP archive (*.zip)|*.zip|All files (*.*)|*.*",
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog(this) == true)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, bytes);
                    StatusText.Text = $"Archive exported • {Path.GetFileName(dialog.FileName)}";
                    await LoadArchiveAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Archive", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportAdvancedAsync(string endpoint, string defaultName, string extension)
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
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = defaultName,
                    Filter = extension == "pdf" ? "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*" : "Excel files (*.xls)|*.xls|All files (*.*)|*.*",
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

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) yield return typed;
                foreach (T descendant in FindVisualChildren<T>(child)) yield return descendant;
            }
        }
    }
}
