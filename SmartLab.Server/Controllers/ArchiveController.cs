using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ArchiveController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ArchiveController(AppDbContext context) => _context = context;

        [HttpGet("plan")]
        public async Task<IActionResult> Plan([FromQuery] int ageDays = 60)
        {
            ageDays = Math.Clamp(ageDays, 30, 3650);
            DateTime cutoff = DateTime.Now.AddDays(-ageDays);
            int sessions = await _context.PcUsageHistory.CountAsync(s => s.LogoutTime.HasValue && s.LogoutTime < cutoff);
            int maintenance = await _context.MaintenanceRecords.CountAsync(m => m.EndedAt.HasValue && m.EndedAt < cutoff);
            int tickets = await _context.ServiceDeskTickets.CountAsync(t => t.ResolvedAt.HasValue && t.ResolvedAt < cutoff);
            int activity = await _context.ActivityLogs.CountAsync(a => a.CreatedAt < cutoff);
            return Ok(new
            {
                cutoff, ageDays,
                usageHistoryCount = sessions,
                maintenanceCount = maintenance,
                serviceDeskCount = tickets,
                activityLogCount = activity,
                totalEligible = sessions + maintenance + tickets + activity,
                eligible = new { usageSessions = sessions, maintenanceRecords = maintenance, serviceDeskTickets = tickets, activityLogs = activity },
                policy = "Export eligible records before any deletion. Active operational records are never archived by this endpoint."
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] int take = 50)
        {
            take = Math.Clamp(take, 1, 200);
            var rows = await _context.ActivityLogs.AsNoTracking()
                .Where(a => a.Action == "ArchiveExport")
                .OrderByDescending(a => a.CreatedAt)
                .Take(take)
                .Select(a => new { a.ActivityLogId, a.UserId, a.Action, a.Details, a.CreatedAt })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpPost("run")]
        public async Task<IActionResult> Run([FromQuery] int ageDays = 60)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId))
                return Unauthorized();

            ageDays = Math.Clamp(ageDays, 30, 3650);
            DateTime cutoff = DateTime.Now.AddDays(-ageDays);

            var usage = await _context.PcUsageHistory.AsNoTracking()
                .Where(s => s.LogoutTime.HasValue && s.LogoutTime < cutoff)
                .OrderBy(s => s.LoginTime)
                .Select(s => new { s.SessionId, PC = s.PC != null ? s.PC.PCNumber : null, Student = s.User != null ? s.User.Username : null, Lab = s.Laboratory != null ? s.Laboratory.LabName : null, s.LoginTime, s.LogoutTime, s.DurationSeconds, s.EndReason })
                .ToListAsync();

            var maintenance = await _context.MaintenanceRecords.AsNoTracking()
                .Where(m => m.EndedAt.HasValue && m.EndedAt < cutoff)
                .OrderBy(m => m.StartedAt)
                .Select(m => new { m.MaintenanceRecordId, PC = m.PC != null ? m.PC.PCNumber : null, m.Reason, m.StartedAt, m.EndedAt, Technician = m.TechnicianUser != null ? m.TechnicianUser.Username : null, m.Notes })
                .ToListAsync();

            var serviceDesk = await _context.ServiceDeskTickets.AsNoTracking()
                .Where(t => t.ResolvedAt.HasValue && t.ResolvedAt < cutoff)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new { t.ServiceDeskTicketId, t.TeacherUsername, t.PCNumber, t.Location, t.Category, t.Subject, t.Status, t.CreatedAt, t.StartedAt, t.ResolvedAt, t.ResolutionNotes })
                .ToListAsync();

            var activity = await _context.ActivityLogs.AsNoTracking()
                .Where(a => a.CreatedAt < cutoff)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new { a.ActivityLogId, a.UserId, a.PCId, a.Action, a.Details, a.CreatedAt })
                .ToListAsync();

            string filename = $"smartlab-archive-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            byte[] archiveBytes;
            using (var memory = new MemoryStream())
            {
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
                {
                    AddEntry(zip, "usage.csv", BuildCsv(new[] { "SessionId", "PC", "Student", "Laboratory", "LoginTime", "LogoutTime", "DurationSeconds", "EndReason" }, usage.Select(s => new[] { s.SessionId.ToString(), s.PC ?? "", s.Student ?? "", s.Lab ?? "", s.LoginTime.ToString("O"), s.LogoutTime?.ToString("O") ?? "", s.DurationSeconds?.ToString() ?? "", s.EndReason }).ToList()));
                    AddEntry(zip, "maintenance.csv", BuildCsv(new[] { "MaintenanceRecordId", "PC", "Reason", "StartedAt", "EndedAt", "Technician", "Notes" }, maintenance.Select(m => new[] { m.MaintenanceRecordId.ToString(), m.PC ?? "", m.Reason, m.StartedAt.ToString("O"), m.EndedAt?.ToString("O") ?? "", m.Technician ?? "", m.Notes ?? "" }).ToList()));
                    AddEntry(zip, "service-desk.csv", BuildCsv(new[] { "TicketId", "Teacher", "PC", "Location", "Category", "Subject", "Status", "CreatedAt", "StartedAt", "ResolvedAt", "ResolutionNotes" }, serviceDesk.Select(t => new[] { t.ServiceDeskTicketId.ToString(), t.TeacherUsername, t.PCNumber ?? "", t.Location ?? "", t.Category, t.Subject, t.Status, t.CreatedAt.ToString("O"), t.StartedAt?.ToString("O") ?? "", t.ResolvedAt?.ToString("O") ?? "", t.ResolutionNotes ?? "" }).ToList()));
                    AddEntry(zip, "activity.csv", BuildCsv(new[] { "ActivityLogId", "UserId", "PCId", "Action", "Details", "CreatedAt" }, activity.Select(a => new[] { a.ActivityLogId.ToString(), a.UserId?.ToString() ?? "", a.PCId?.ToString() ?? "", a.Action, a.Details, a.CreatedAt.ToString("O") }).ToList()));
                    AddEntry(zip, "manifest.txt", $"CreatedAt={DateTime.Now:O}\nCutoff={cutoff:O}\nAgeDays={ageDays}\nUsage={usage.Count}\nMaintenance={maintenance.Count}\nServiceDesk={serviceDesk.Count}\nActivity={activity.Count}\nPolicy=Export-first; no database rows deleted.\n");
                }
                archiveBytes = memory.ToArray();
            }

            string sha256 = Convert.ToHexString(SHA256.HashData(archiveBytes));
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = adminUserId,
                Action = "ArchiveExport",
                Details = $"Created {filename}; cutoff={cutoff:O}; ageDays={ageDays}; usage={usage.Count}; maintenance={maintenance.Count}; serviceDesk={serviceDesk.Count}; activity={activity.Count}; sha256={sha256}",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return File(archiveBytes, "application/zip", filename);
        }

        [HttpGet("usage.csv")]
        public async Task<IActionResult> UsageArchiveCsv([FromQuery] int ageDays = 60)
        {
            ageDays = Math.Clamp(ageDays, 30, 3650);
            DateTime cutoff = DateTime.Now.AddDays(-ageDays);
            var rows = await _context.PcUsageHistory.AsNoTracking().Where(s => s.LogoutTime.HasValue && s.LogoutTime < cutoff).OrderBy(s => s.LoginTime)
                .Select(s => new { s.SessionId, PC = s.PC != null ? s.PC.PCNumber : null, Student = s.User != null ? s.User.Username : null, Laboratory = s.Laboratory != null ? s.Laboratory.LabName : null, s.LoginTime, s.LogoutTime, s.DurationSeconds, s.EndReason }).ToListAsync();
            string csv = BuildCsv(new[] { "SessionId", "PC", "Student", "Laboratory", "LoginTime", "LogoutTime", "DurationSeconds", "EndReason" }, rows.Select(s => new[] { s.SessionId.ToString(CultureInfo.InvariantCulture), s.PC ?? "", s.Student ?? "", s.Laboratory ?? "", s.LoginTime.ToString("O"), s.LogoutTime?.ToString("O") ?? "", s.DurationSeconds?.ToString(CultureInfo.InvariantCulture) ?? "", s.EndReason }).ToList());
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"smartlab-archive-usage-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string BuildCsv(string[] headers, List<string[]> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", headers.Select(Escape)));
            foreach (var row in rows) builder.AppendLine(string.Join(",", row.Select(Escape)));
            return builder.ToString();
        }

        private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
