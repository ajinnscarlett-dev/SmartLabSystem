using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client
{
    public partial class AssistanceDialog : Window
    {
        public string Category { get; private set; } = "General";
        public string Description { get; private set; } = string.Empty;

        public AssistanceDialog()
        {
            InitializeComponent();
            DescriptionTextBox.Focus();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string description = DescriptionTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(
                    "Please describe the problem before sending the request.",
                    "Need Assistance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Category =
                (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? "General";

            Description = description;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
