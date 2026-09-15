using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    public partial class AdminDashboard
    {
        private readonly bool _operationsNavigationAttached = AttachOperationsNavigation();

        private bool AttachOperationsNavigation()
        {
            AddHandler(Button.ClickEvent, new RoutedEventHandler(OperationsNavigation_Click));
            return true;
        }

        private void OperationsNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button)
                return;

            string label = GetButtonLabel(button);

            if (string.Equals(label, "Incidents", StringComparison.OrdinalIgnoreCase))
            {
                OpenServiceDeskCenter();
                return;
            }

            if (string.Equals(label, "Reports", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Maintenance", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Announcements", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Send Announcement", StringComparison.OrdinalIgnoreCase))
            {
                OpenOperationsCenter();
            }
        }

        private string GetButtonLabel(Button button)
        {
            if (button.Content is string text)
                return text.Trim();

            if (button.Content is Panel panel)
            {
                return string.Join(" ", panel.Children.OfType<TextBlock>()
                    .Select(t => t.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t)))
                    .Trim();
            }

            return string.Empty;
        }

        private void OpenOperationsCenter()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is AdminOperationsWindow existing && existing.IsVisible)
                {
                    existing.Activate();
                    return;
                }
            }

            new AdminOperationsWindow(AdminNameText.Text)
            {
                Owner = this
            }.Show();
        }

        private void OpenServiceDeskCenter()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is AdminServiceDeskWindow existing && existing.IsVisible)
                {
                    existing.Activate();
                    return;
                }
            }

            new AdminServiceDeskWindow
            {
                Owner = this
            }.Show();
        }
    }
}
