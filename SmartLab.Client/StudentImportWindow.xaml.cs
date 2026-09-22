using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace SmartLab.Client;

public partial class StudentImportWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<StudentExcelImportRow> _rows;

    public StudentImportWindow(Window owner, HttpClient httpClient, IReadOnlyList<StudentExcelImportRow> rows, string fileName)
    {
        InitializeComponent();
        Owner = owner;
        _httpClient = httpClient;
        _rows = rows;
        FileText.Text = fileName;
        RowsGrid.ItemsSource = _rows;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        int valid = _rows.Count(r => r.IsValid);
        int rejected = _rows.Count - valid;
        SummaryText.Text = $"{_rows.Count:N0} data row(s) found  |  {valid:N0} ready  |  {rejected:N0} locally rejected";
        ImportButton.IsEnabled = valid > 0;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        List<StudentImportRequestRow> validRows = _rows
            .Where(r => r.IsValid)
            .Select(r => new StudentImportRequestRow
            {
                RowNumber = r.ExcelRow,
                StudentNumber = r.StudentNumber,
                StudentName = r.StudentName
            })
            .ToList();

        if (validRows.Count == 0)
            return;

        ImportButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        StatusText.Text = "Importing...";

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/User/management/students/import",
                new { Rows = validRows });

            string responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    ExtractMessage(responseText, "Student import failed."),
                    "Import Students",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            using JsonDocument json = JsonDocument.Parse(responseText);
            int imported = GetInt(json.RootElement, "importedCount");
            int rejected = GetInt(json.RootElement, "rejectedCount");

            string summary = $"Imported: {imported:N0}\nRejected by server: {rejected:N0}";
            if (rejected > 0 && json.RootElement.TryGetProperty("rejected", out JsonElement rejectedRows))
            {
                List<string> details = new();
                foreach (JsonElement item in rejectedRows.EnumerateArray().Take(20))
                {
                    int row = GetInt(item, "rowNumber");
                    string number = GetString(item, "studentNumber");
                    string reason = GetString(item, "reason");
                    details.Add($"Row {row}: {number} — {reason}");
                }

                if (details.Count > 0)
                    summary += "\n\n" + string.Join("\n", details);
            }

            MessageBox.Show(
                summary,
                "Student Import Complete",
                MessageBoxButton.OK,
                rejected == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

            DialogResult = imported > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Student import failed.\n\n{ex.Message}",
                "Import Students",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (IsVisible)
            {
                ImportButton.IsEnabled = _rows.Any(r => r.IsValid);
                CancelButton.IsEnabled = true;
                StatusText.Text = "Ready";
            }
        }
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : 0;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ExtractMessage(string text, string fallback)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            return GetString(json.RootElement, "message") is { Length: > 0 } message
                ? message
                : fallback;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }

    private sealed class StudentImportRequestRow
    {
        public int RowNumber { get; init; }
        public string StudentNumber { get; init; } = string.Empty;
        public string StudentName { get; init; } = string.Empty;
    }
}
