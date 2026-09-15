using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("usage.csv")]
        public async Task<IActionResult> UsageCsv(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? laboratoryId = null)
        {
            DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-30);
            DateTime end = to?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

            var rows = await _context.PcUsageHistory
                .AsNoTracking()
                .Where(s =>
                    s.LoginTime >= start &&
                    s.LoginTime < end &&
                    (!laboratoryId.HasValue || s.LaboratoryId == laboratoryId.Value))
                .OrderBy(s => s.LoginTime)
                .Select(s => new
                {
                    s.SessionId,
                    PC = s.PC != null ? s.PC.PCNumber : null,
                    Student = s.User != null ? s.User.Username : null,
                    Laboratory = s.Laboratory != null ? s.Laboratory.LabName : null,
                    s.LoginTime,
                    s.LogoutTime,
                    s.DurationSeconds,
                    s.EndReason
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("SessionId,PC,Student,Laboratory,LoginTime,LogoutTime,DurationSeconds,EndReason");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Escape(row.SessionId.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.PC),
                    Escape(row.Student),
                    Escape(row.Laboratory),
                    Escape(row.LoginTime.ToString("O")),
                    Escape(row.LogoutTime?.ToString("O")),
                    Escape(row.DurationSeconds?.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.EndReason)
                }));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"smartlab-usage-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpGet("maintenance.csv")]
        public async Task<IActionResult> MaintenanceCsv(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-30);
            DateTime end = to?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

            var rows = await _context.MaintenanceRecords
                .AsNoTracking()
                .Where(m => m.StartedAt >= start && m.StartedAt < end)
                .OrderBy(m => m.StartedAt)
                .Select(m => new
                {
                    m.MaintenanceRecordId,
                    PC = m.PC != null ? m.PC.PCNumber : null,
                    m.Reason,
                    m.StartedAt,
                    m.EndedAt,
                    Technician = m.TechnicianUser != null ? m.TechnicianUser.Username : null,
                    m.Notes
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("MaintenanceRecordId,PC,Reason,StartedAt,EndedAt,Technician,Notes");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Escape(row.MaintenanceRecordId.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.PC),
                    Escape(row.Reason),
                    Escape(row.StartedAt.ToString("O")),
                    Escape(row.EndedAt?.ToString("O")),
                    Escape(row.Technician),
                    Escape(row.Notes)
                }));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"smartlab-maintenance-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpGet("inventory.csv")]
        public async Task<IActionResult> InventoryCsv()
        {
            var rows = await _context.HardwareInventories
                .AsNoTracking()
                .OrderBy(h => h.PCId)
                .Select(h => new
                {
                    PC = h.PC != null ? h.PC.PCNumber : null,
                    LaboratoryId = h.PC != null ? h.PC.LaboratoryId : null,
                    h.Cpu,
                    h.Ram,
                    h.Storage,
                    h.Gpu,
                    h.OperatingSystem,
                    h.MACAddress,
                    h.IPAddress,
                    h.LastAuditedAt
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("PC,LaboratoryId,CPU,RAM,Storage,GPU,OperatingSystem,MACAddress,IPAddress,LastAuditedAt");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Escape(row.PC),
                    Escape(row.LaboratoryId?.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.Cpu),
                    Escape(row.Ram),
                    Escape(row.Storage),
                    Escape(row.Gpu),
                    Escape(row.OperatingSystem),
                    Escape(row.MACAddress),
                    Escape(row.IPAddress),
                    Escape(row.LastAuditedAt.ToString("O"))
                }));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"smartlab-inventory-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpGet("service-desk.csv")]
        public async Task<IActionResult> ServiceDeskCsv()
        {
            var rows = await _context.ServiceDeskTickets
                .AsNoTracking()
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    t.ServiceDeskTicketId,
                    t.TeacherUsername,
                    t.PCNumber,
                    t.Location,
                    t.Category,
                    t.Subject,
                    t.Description,
                    t.Status,
                    t.AssignedToUsername,
                    t.CreatedAt,
                    t.StartedAt,
                    t.ResolvedAt,
                    t.ResolutionNotes
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("TicketId,Teacher,PC,Location,Category,Subject,Description,Status,AssignedTo,CreatedAt,StartedAt,ResolvedAt,ResolutionNotes");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Escape(row.ServiceDeskTicketId.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.TeacherUsername),
                    Escape(row.PCNumber),
                    Escape(row.Location),
                    Escape(row.Category),
                    Escape(row.Subject),
                    Escape(row.Description),
                    Escape(row.Status),
                    Escape(row.AssignedToUsername),
                    Escape(row.CreatedAt.ToString("O")),
                    Escape(row.StartedAt?.ToString("O")),
                    Escape(row.ResolvedAt?.ToString("O")),
                    Escape(row.ResolutionNotes)
                }));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"smartlab-service-desk-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpGet("activity.csv")]
        public async Task<IActionResult> ActivityCsv(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-30);
            DateTime end = to?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

            var rows = await _context.ActivityLogs
                .AsNoTracking()
                .Where(a => a.CreatedAt >= start && a.CreatedAt < end)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new
                {
                    a.ActivityLogId,
                    a.UserId,
                    a.PCId,
                    a.Action,
                    a.Details,
                    a.CreatedAt
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("ActivityLogId,UserId,PCId,Action,Details,CreatedAt");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Escape(row.ActivityLogId.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.UserId?.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.PCId?.ToString(CultureInfo.InvariantCulture)),
                    Escape(row.Action),
                    Escape(row.Details),
                    Escape(row.CreatedAt.ToString("O"))
                }));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"smartlab-activity-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        private static string Escape(string? value)
        {
            string text = value ?? string.Empty;
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
    }
}
