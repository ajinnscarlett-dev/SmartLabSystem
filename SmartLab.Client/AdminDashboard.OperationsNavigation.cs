using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    public partial class AdminDashboard
    {
        private void SelectLaboratoryFromNavigation(string laboratoryName)
        {
            if (LaboratorySelectorCombo == null)
                return;

            LaboratorySelectorItem? target =
                LaboratorySelectorCombo.Items
                    .OfType<LaboratorySelectorItem>()
                    .FirstOrDefault(item =>
                        string.Equals(
                            item.LabName,
                            laboratoryName,
                            StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                LaboratorySelectorCombo.SelectedItem = target;
                return;
            }

            // The selector is loaded asynchronously. Keep the requested lab
            // in the dashboard state when it is not yet available in the UI.
            string requestedName = laboratoryName.Trim();
            _selectedLaboratoryId = null;
            foreach (LaboratorySelectorItem item in LaboratorySelectorCombo.Items.OfType<LaboratorySelectorItem>())
            {
                if (string.Equals(item.LabName, requestedName, StringComparison.OrdinalIgnoreCase))
                {
                    LaboratorySelectorCombo.SelectedItem = item;
                    return;
                }
            }
        }

        private void ComLab101NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 101");

        private void ComLab601NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 601");

        private void ComLab602NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 602");

        private void ComLab603NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 603");

        private void ComLab604NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 604");

        private void ComLab605NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 605");

        private void ComLab607NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 607");

        private void ComLab609NavButton_Click(object sender, RoutedEventArgs e) =>
            SelectLaboratoryFromNavigation("ComLab 609");

        private void BlockThisUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPc == null || !_selectedPc.CurrentUserId.HasValue)
            {
                MessageBox.Show(
                    "Select an occupied PC before blocking the current user.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                "Blocking a student account is managed through User Management. The selected PC remains unchanged.",
                "SmartLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void PcDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPc == null)
            {
                MessageBox.Show(
                    "Select a PC first.",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ShowPCDetails(_selectedPc);
        }

        private void SessionHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOperationsCenter(1);
        }

        private void ActivityLogsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOperationsCenter(6);
        }

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

            if (selectedTabIndex.HasValue)
            {
                operationsWindow.OperationsTabs.SelectedIndex =
                    Math.Max(0, selectedTabIndex.Value);
            }
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
