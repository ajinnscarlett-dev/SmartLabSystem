using Microsoft.Win32;
using System.Windows;

namespace SmartLab.Client;

public partial class UserManagementWindow
{
    private void ImportStudentsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Student Excel File",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            IReadOnlyList<StudentExcelImportRow> rows = StudentExcelImportService.ReadStudents(dialog.FileName);
            var importWindow = new StudentImportWindow(this, _httpClient, rows, System.IO.Path.GetFileName(dialog.FileName));

            bool? imported = importWindow.ShowDialog();
            if (imported == true)
                _ = LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Import Students",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
