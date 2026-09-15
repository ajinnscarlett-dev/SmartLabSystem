using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartLab.Client
{
    public partial class AdminDashboard
    {
        private static readonly bool NavigationHandlerRegistered = RegisterNavigationHandler();

        private static bool RegisterNavigationHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(AdminDashboardLoadedForNavigation));
            return true;
        }

        private static void AdminDashboardLoadedForNavigation(object sender, RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
                return;

            dashboard.NormalizeAdminNavigation();
        }

        private void NormalizeAdminNavigation()
        {
            Button? schedulesButton = FindButtonByLabel("Schedules");
            if (schedulesButton != null)
            {
                schedulesButton.Visibility = Visibility.Collapsed;
                schedulesButton.Click -= ReportsNavButton_Click;
            }

            Button? maintenanceButton = FindButtonByLabel("Maintenance");
            if (maintenanceButton != null)
            {
                maintenanceButton.Click -= PcManagementNavButton_Click;
                maintenanceButton.Click -= MaintenanceNavButton_Click;
                maintenanceButton.Click += MaintenanceNavButton_Click;
            }

            BlockThisUserButton?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            ApplyConsistentNavigationIcons();
        }

        private Button? FindButtonByLabel(string label)
        {
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                if (string.Equals(GetButtonLabel(button), label, StringComparison.OrdinalIgnoreCase))
                    return button;
            }

            return null;
        }

        private static string GetButtonLabel(Button button)
        {
            if (button.Content is string text)
                return text.Trim();

            if (button.Content is Panel panel)
            {
                return string.Join(" ", panel.Children
                    .OfType<TextBlock>()
                    .Select(t => t.Text?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            return string.Empty;
        }

        private void ApplyConsistentNavigationIcons()
        {
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                string label = GetButtonLabel(button);
                if (!IsPrimaryNavigationLabel(label))
                    continue;

                if (button.Content is not StackPanel panel || panel.Orientation != Orientation.Horizontal)
                    continue;

                if (panel.Children.Count == 0 || panel.Children[0] is not TextBlock)
                    continue;

                Path icon = new Path
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 10, 0),
                    Stretch = Stretch.Uniform,
                    Fill = new SolidColorBrush(Color.FromRgb(151, 166, 176)),
                    Data = CreateNavigationGeometry(label)
                };

                panel.Children.RemoveAt(0);
                panel.Children.Insert(0, icon);
            }
        }

        private static bool IsPrimaryNavigationLabel(string label) =>
            label.Equals("Dashboard", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Users", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Laboratories", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Computers", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Incidents", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Reports", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Announcements", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Settings", StringComparison.OrdinalIgnoreCase) ||
            label.StartsWith("ComLab ", StringComparison.OrdinalIgnoreCase);

        private static Geometry CreateNavigationGeometry(string label)
        {
            string data = label.ToLowerInvariant() switch
            {
                "dashboard" => "M2,8 L9,2 L16,8 V16 H11 V11 H7 V16 H2 Z",
                "users" => "M4,3 H14 A2,2 0 0 1 16,5 V15 H4 Z M6,6 H12 M6,9 H12 M6,12 H10",
                "laboratories" => "M2,16 V3 H14 V16 M5,6 H8 M5,9 H8 M5,12 H8 M11,6 H13 M11,9 H13 M11,12 H13",
                "computers" => "M2,3 H16 V13 H2 Z M7,16 H11 M9,13 V16",
                "incidents" => "M9,1 L16,15 H2 Z M9,5 V10 M9,13 V14",
                "maintenance" => "M4,3 L7,6 L13,2 L15,4 L11,8 L14,11 L12,13 L9,10 L6,14 L4,12 L7,9 L3,5 Z",
                "reports" => "M4,2 H14 V16 H4 Z M7,5 H12 M7,8 H12 M7,11 H10",
                "announcements" => "M2,6 H5 L12,2 V14 L5,10 H2 Z M14,6 H16 V10 H14",
                "settings" => "M8,2 H10 V5 C11,5 12,6 13,7 L15,6 L16,8 L14,9 C14,10 14,11 13,12 L14,14 L12,16 L10,14 C9,14 8,14 7,13 L5,15 L3,13 L5,11 C4,10 4,9 5,8 L3,6 L5,4 L7,5 C8,4 9,4 10,4 Z M9,8 A2,2 0 1 0 9,12 A2,2 0 1 0 9,8",
                _ when label.StartsWith("ComLab ", StringComparison.OrdinalIgnoreCase) => "M2,3 H16 V16 H2 Z M5,6 H8 M10,6 H13 M5,10 H8 M10,10 H13 M7,13 H11",
                _ => "M3,3 H15 V15 H3 Z"
            };

            return Geometry.Parse(data);
        }

        private void MaintenanceNavButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOperationsCenter(2);
        }

        private void SelectLaboratoryFromNavigation(string laboratoryName)
        {
            if (LaboratorySelectorCombo == null)
                return;

            LaboratorySelectorItem? target = LaboratorySelectorCombo.Items
                .OfType<LaboratorySelectorItem>()
                .FirstOrDefault(item => string.Equals(
                    item.LabName,
                    laboratoryName,
                    StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                LaboratorySelectorCombo.SelectedItem = target;
                return;
            }

            _selectedLaboratoryId = null;
        }

        private void ComLab101NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 101");
        private void ComLab601NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 601");
        private void ComLab602NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 602");
        private void ComLab603NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 603");
        private void ComLab604NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 604");
        private void ComLab605NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 605");
        private void ComLab607NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 607");
        private void ComLab609NavButton_Click(object sender, RoutedEventArgs e) => SelectLaboratoryFromNavigation("ComLab 609");

        private void BlockThisUserButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Account blocking is managed through User Management.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void PcDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show("Select a PC first.", "SmartLab", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowPCDetails(_selectedPc);
        }

        private void SessionHistoryButton_Click(object sender, RoutedEventArgs e) => OpenOperationsCenter(1);
        private void ActivityLogsButton_Click(object sender, RoutedEventArgs e) => OpenOperationsCenter(6);

        private void OpenOperationsCenter(int? selectedTabIndex = null)
        {
            AdminOperationsWindow? operationsWindow = null;

            foreach (Window window in Application.Current.Windows)
            {
                if (window is AdminOperationsWindow existing && existing.IsVisible)
                {
                    operationsWindow = existing;
                    break;
                }
            }

            if (operationsWindow == null)
            {
                operationsWindow = new AdminOperationsWindow(AdminNameText.Text)
                {
                    Owner = this
                };
                operationsWindow.Show();
            }
            else
            {
                operationsWindow.Activate();
            }

            if (selectedTabIndex.HasValue && operationsWindow.OperationsTabs.Items.Count > selectedTabIndex.Value)
                operationsWindow.OperationsTabs.SelectedIndex = selectedTabIndex.Value;
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

            new AdminServiceDeskWindow { Owner = this }.Show();
        }
    }
}