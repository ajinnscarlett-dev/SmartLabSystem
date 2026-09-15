using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/ReportFiles")]
    [Authorize(Roles = "Admin")]
    public class ReportFilesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportFilesController(AppDbContext context) => _context = context;

        [HttpGet("{report}.{format}")]
        public async Task<IActionResult> Export(string report, string format)
        {
            report = report.Trim().ToLowerInvariant();
            format = format.Trim().ToLowerInvariant();

            if (format is not ("xls" or "pdf"))
                return BadRequest(new { message = "Supported formats are xls and pdf." });

            var table = await BuildTableAsync(report);
            if (table is null)
                return NotFound(new { message = "Unknown report type." });

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            if (format == "xls")
            {
                byte[] bytes = Encoding.UTF8.GetBytes(BuildExcelHtml(table.Value.Headers, table.Value.Rows));
                return File(bytes, "application/vnd.ms-excel", $"smartlab-{report}-{timestamp}.xls");
            }

            return File(BuildPdf(table.Value.Headers, table.Value.Rows), "application/pdf", $"smartlab-{report}-{timestamp}.pdf");
        }

        private async Task<(string[] Headers, List<string[]> Rows)?> BuildTableAsync(string report)
        {
            switch (report)
            {
                case "usage":
                {
                    var raw = await _context.PcUsageHistory.AsNoTracking().OrderByDescending(s => s.LoginTime).Take(5000)
                        .Select(s => new { s.SessionId, PC = s.PC != null ? s.PC.PCNumber : null, Student = s.User != null ? s.User.Username : null, Laboratory = s.Laboratory != null ? s.Laboratory.LabName : null, s.LoginTime, s.LogoutTime, s.DurationSeconds, s.EndReason })
                        .ToListAsync();
                    return (new[] { "SessionId", "PC", "Student", "Laboratory", "LoginTime", "LogoutTime", "DurationSeconds", "EndReason" }, raw.Select(s => new[] { s.SessionId.ToString(), s.PC ?? "", s.Student ?? "", s.Laboratory ?? "", s.LoginTime.ToString("yyyy-MM-dd HH:mm:ss"), s.LogoutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", s.DurationSeconds?.ToString() ?? "", s.EndReason }).ToList());
                }
                case "maintenance":
                {
                    var raw = await _context.MaintenanceRecords.AsNoTracking().OrderByDescending(m => m.StartedAt).Take(5000)
                        .Select(m => new { m.MaintenanceRecordId, PC = m.PC != null ? m.PC.PCNumber : null, m.Reason, m.StartedAt, m.EndedAt, Technician = m.TechnicianUser != null ? m.TechnicianUser.Username : null, m.Notes }).ToListAsync();
                    return (new[] { "MaintenanceRecordId", "PC", "Reason", "StartedAt", "EndedAt", "Technician", "Notes" }, raw.Select(m => new[] { m.MaintenanceRecordId.ToString(), m.PC ?? "", m.Reason, m.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"), m.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", m.Technician ?? "", m.Notes ?? "" }).ToList());
                }
                case "inventory":
                {
                    var raw = await _context.HardwareInventories.AsNoTracking().OrderBy(h => h.PCId).Take(5000)
                        .Select(h => new { PC = h.PC != null ? h.PC.PCNumber : null, Laboratory = h.PC != null && h.PC.Laboratory != null ? h.PC.Laboratory.LabName : null, h.Cpu, h.Ram, h.Storage, h.Gpu, h.OperatingSystem, h.MACAddress, h.IPAddress, h.LastAuditedAt }).ToListAsync();
                    return (new[] { "PC", "Laboratory", "CPU", "RAM", "Storage", "GPU", "OperatingSystem", "MACAddress", "IPAddress", "LastAuditedAt" }, raw.Select(h => new[] { h.PC ?? "", h.Laboratory ?? "", h.Cpu ?? "", h.Ram ?? "", h.Storage ?? "", h.Gpu ?? "", h.OperatingSystem ?? "", h.MACAddress ?? "", h.IPAddress ?? "", h.LastAuditedAt.ToString("yyyy-MM-dd HH:mm:ss") }).ToList());
                }
                case "service-desk":
                {
                    var raw = await _context.ServiceDeskTickets.AsNoTracking().OrderByDescending(t => t.CreatedAt).Take(5000)
                        .Select(t => new { t.ServiceDeskTicketId, t.TeacherUsername, t.PCNumber, t.Location, t.Category, t.Subject, t.Description, t.Status, t.AssignedToUsername, t.CreatedAt, t.StartedAt, t.ResolvedAt, t.ResolutionNotes }).ToListAsync();
                    return (new[] { "TicketId", "Teacher", "PC", "Location", "Category", "Subject", "Description", "Status", "AssignedTo", "CreatedAt", "StartedAt", "ResolvedAt", "ResolutionNotes" }, raw.Select(t => new[] { t.ServiceDeskTicketId.ToString(), t.TeacherUsername, t.PCNumber ?? "", t.Location ?? "", t.Category, t.Subject, t.Description, t.Status, t.AssignedToUsername ?? "", t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), t.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", t.ResolvedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", t.ResolutionNotes ?? "" }).ToList());
                }
                case "activity":
                {
                    var raw = await _context.ActivityLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(5000)
                        .Select(a => new { a.ActivityLogId, a.UserId, a.PCId, a.Action, a.Details, a.CreatedAt }).ToListAsync();
                    return (new[] { "ActivityLogId", "UserId", "PCId", "Action", "Details", "CreatedAt" }, raw.Select(a => new[] { a.ActivityLogId.ToString(), a.UserId?.ToString() ?? "", a.PCId?.ToString() ?? "", a.Action, a.Details, a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") }).ToList());
                }
                default:
                    return null;
            }
        }

        private static string BuildExcelHtml(string[] headers, List<string[]> rows)
        {
            var builder = new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>table{border-collapse:collapse}th,td{border:1px solid #999;padding:4px;font-family:Segoe UI,Arial;font-size:10pt}th{font-weight:bold;background:#eaeaea}</style></head><body><table>");
            builder.Append("<tr>"); foreach (string header in headers) builder.Append("<th>").Append(HtmlEncode(header)).Append("</th>"); builder.Append("</tr>");
            foreach (string[] row in rows) { builder.Append("<tr>"); foreach (string cell in row) builder.Append("<td>").Append(HtmlEncode(cell)).Append("</td>"); builder.Append("</tr>"); }
            builder.Append("</table></body></html>");
            return builder.ToString();
        }

        private static byte[] BuildPdf(string[] headers, List<string[]> rows)
        {
            const int maxRows = 45;
            var lines = new List<string> { string.Join(" | ", headers) };
            lines.AddRange(rows.Take(maxRows - 1).Select(r => string.Join(" | ", r.Select(CompactPdfText))));

            var content = new StringBuilder("BT /F1 8 Tf 32 760 Td");
            for (int i = 0; i < lines.Count; i++) { if (i > 0) content.Append(" 0 -15 Td"); content.Append(" (").Append(PdfEscape(Truncate(lines[i], 180))).Append(") Tj"); }
            content.Append(" ET");
            string stream = content.ToString();

            var document = new StringBuilder();
            var offsets = new List<int> { 0 };
            document.Append("%PDF-1.4\n");
            void AddObject(int number, string body) { offsets.Add(Encoding.ASCII.GetByteCount(document.ToString())); document.Append(number).Append(" 0 obj\n").Append(body).Append("\nendobj\n"); }
            AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
            AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
            AddObject(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            byte[] streamBytes = Encoding.ASCII.GetBytes(stream);
            AddObject(5, $"<< /Length {streamBytes.Length} >>\nstream\n{stream}\nendstream");
            int xrefOffset = Encoding.ASCII.GetByteCount(document.ToString());
            document.Append("xref\n0 6\n0000000000 65535 f \n");
            for (int i = 1; i <= 5; i++) document.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            document.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF");
            return Encoding.ASCII.GetBytes(document.ToString());
        }

        private static string CompactPdfText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Replace("\r", " ").Replace("\n", " ");
        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "?";
        private static string PdfEscape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
