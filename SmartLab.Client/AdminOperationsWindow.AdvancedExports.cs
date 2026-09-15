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
            Loaded += AddAdvancedExportButtons;
        }

        private void AddAdvancedExportButtons(object? sender, RoutedEventArgs e)
        {
            Loaded -= AddAdvancedExportButtons;
            TabItem? exportsTab = null;
            foreach (TabItem tab in FindVisualChildren<TabItem>(this))
            {
                if (string.Equals(tab.Header?.ToString(), "EXPORTS", StringComparison.OrdinalIgnoreCase))
                {
                    exportsTab = tab;
                    break;
                }
            }

            if (exportsTab == null) return;

            WrapPanel? panel = FindVisualChildren<WrapPanel>(exportsTab).FirstOrDefault();
            if (panel == null) return;

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
