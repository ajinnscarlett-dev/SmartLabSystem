using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace SmartLab.Client;

public sealed class StudentExcelImportRow
{
    public int ExcelRow { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string Validation { get; init; } = string.Empty;
    public bool IsValid => string.IsNullOrWhiteSpace(Validation);
}

public static class StudentExcelImportService
{
    private const int MaxRows = 2000;

    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<StudentExcelImportRow> ReadStudents(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("An Excel file is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected Excel file could not be found.", filePath);

        if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SmartLab student import currently supports .xlsx Excel files.");

        using FileStream stream = File.OpenRead(filePath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        Dictionary<int, string> sharedStrings = ReadSharedStrings(archive);
        string sheetPath = ResolveFirstWorksheetPath(archive);
        XDocument sheet = LoadXml(archive, sheetPath);

        XElement? headerRow = null;
        int studentNumberColumn = -1;
        int studentNameColumn = -1;

        foreach (XElement row in sheet.Descendants(SpreadsheetNamespace + "row").Take(25))
        {
            Dictionary<int, string> values = ReadRowValues(row, sharedStrings);
            if (values.Count == 0)
                continue;

            int numberColumn = FindHeaderColumn(values, IsStudentNumberHeader);
            int nameColumn = FindHeaderColumn(values, IsStudentNameHeader);

            if (numberColumn >= 0 && nameColumn >= 0)
            {
                headerRow = row;
                studentNumberColumn = numberColumn;
                studentNameColumn = nameColumn;
                break;
            }
        }

        if (headerRow == null)
            throw new InvalidOperationException("The first worksheet must contain 'Student Number' and 'Student Name' columns.");

        HashSet<string> seenNumbers = new(StringComparer.OrdinalIgnoreCase);
        List<StudentExcelImportRow> rows = new();

        foreach (XElement row in sheet.Descendants(SpreadsheetNamespace + "row"))
        {
            if (ReferenceEquals(row, headerRow))
                continue;

            int rowNumber = ParseRowNumber(row.Attribute("r")?.Value, rows.Count + 2);
            if (rows.Count >= MaxRows)
                throw new InvalidOperationException($"The import is limited to {MaxRows:N0} data rows per file.");

            Dictionary<int, string> values = ReadRowValues(row, sharedStrings);
            if (values.Values.All(string.IsNullOrWhiteSpace))
                continue;

            string studentNumber = values.GetValueOrDefault(studentNumberColumn)?.Trim() ?? string.Empty;
            string studentName = values.GetValueOrDefault(studentNameColumn)?.Trim() ?? string.Empty;
            string validation = string.Empty;

            if (string.IsNullOrWhiteSpace(studentNumber))
                validation = "Missing Student Number.";
            else if (string.IsNullOrWhiteSpace(studentName))
                validation = "Missing Student Name.";
            else if (!seenNumbers.Add(studentNumber))
                validation = "Duplicate Student Number in this file.";
            else if (studentNumber.Length > 80)
                validation = "Student Number exceeds 80 characters.";
            else if (studentName.Length > 200)
                validation = "Student Name exceeds 200 characters.";

            rows.Add(new StudentExcelImportRow
            {
                ExcelRow = rowNumber,
                StudentNumber = studentNumber,
                StudentName = studentName,
                Validation = validation
            });
        }

        if (rows.Count == 0)
            throw new InvalidOperationException("No student data rows were found below the header row.");

        return rows;
    }

    private static int FindHeaderColumn(Dictionary<int, string> values, Func<string, bool> predicate)
    {
        foreach (KeyValuePair<int, string> pair in values)
        {
            if (predicate(pair.Value))
                return pair.Key;
        }

        return -1;
    }

    private static bool IsStudentNumberHeader(string value)
    {
        string normalized = NormalizeHeader(value);
        return normalized is "studentnumber" or "studentno" or "studentid";
    }

    private static bool IsStudentNameHeader(string value)
    {
        string normalized = NormalizeHeader(value);
        return normalized is "studentname" or "fullname" or "name";
    }

    private static string NormalizeHeader(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static Dictionary<int, string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return new();

        XDocument document = LoadXml(entry);
        Dictionary<int, string> values = new();
        int index = 0;

        foreach (XElement item in document.Descendants(SpreadsheetNamespace + "si"))
        {
            string text = string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(t => t.Value));
            values[index++] = text;
        }

        return values;
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive)
    {
        XDocument workbook = LoadXml(archive, "xl/workbook.xml");
        XElement? firstSheet = workbook.Descendants(SpreadsheetNamespace + "sheet").FirstOrDefault();
        if (firstSheet == null)
            throw new InvalidOperationException("The workbook does not contain a worksheet.");

        string? relationshipId = firstSheet.Attribute(OfficeRelationshipNamespace + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
            throw new InvalidOperationException("The first worksheet relationship is missing.");

        XDocument relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels", PackageRelationshipNamespace);
        XElement? relationship = relationships.Descendants(PackageRelationshipNamespace + "Relationship")
            .FirstOrDefault(x => string.Equals(x.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal));

        string? target = relationship?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("The first worksheet target could not be resolved.");

        string normalized = target.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "xl/" + normalized;
    }

    private static Dictionary<int, string> ReadRowValues(XElement row, Dictionary<int, string> sharedStrings)
    {
        Dictionary<int, string> values = new();

        foreach (XElement cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            string? reference = cell.Attribute("r")?.Value;
            int column = GetColumnIndex(reference);
            if (column < 0)
                continue;

            string value = ReadCellValue(cell, sharedStrings);
            values[column] = value;
        }

        return values;
    }

    private static string ReadCellValue(XElement cell, Dictionary<int, string> sharedStrings)
    {
        string type = cell.Attribute("t")?.Value ?? string.Empty;

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(t => t.Value));

        string raw = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(raw, out int sharedIndex) &&
            sharedStrings.TryGetValue(sharedIndex, out string? shared))
        {
            return shared;
        }

        return raw;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return -1;

        int index = 0;
        foreach (char character in cellReference)
        {
            if (!char.IsLetter(character))
                break;

            index = index * 26 + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return index > 0 ? index - 1 : -1;
    }

    private static int ParseRowNumber(string? raw, int fallback)
    {
        return int.TryParse(raw, out int rowNumber) && rowNumber > 0
            ? rowNumber
            : fallback;
    }

    private static XDocument LoadXml(ZipArchive archive, string path, XNamespace? relationshipNamespace = null)
    {
        ZipArchiveEntry? entry = archive.GetEntry(path);
        if (entry == null)
            throw new InvalidOperationException($"Required Excel worksheet part '{path}' was not found.");

        return LoadXml(entry);
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }
}
