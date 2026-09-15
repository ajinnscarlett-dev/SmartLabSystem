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
    public class ArchiveController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ArchiveController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("plan")]
        public async Task<IActionResult> Plan([FromQuery] int ageDays = 60)
        {
            ageDays = Math.Clamp(ageDays, 30, 3650);
            DateTime cutoff = DateTime.Now.AddDays(-ageDays);

            int sessions = await _context.PcUsageHistory
                .CountAsync(s => s.LogoutTime.HasValue && s.LogoutTime < cutoff);

            int maintenance = await _context.MaintenanceRecords
                .CountAsync(m => m.EndedAt.HasValue && m.EndedAt < cutoff);

            int tickets = await _context.ServiceDeskTickets
                .CountAsync(t => t.ResolvedAt.HasValue && t.ResolvedAt < cutoff);

            int activity = await _context.ActivityLogs
                .CountAsync(a => a.CreatedAt < cutoff);

            return Ok(new
            {
                cutoff,
                ageDays,
                eligible = new
                {
                    usageSessions = sessions,
                    maintenanceRecords = maintenance,
                    serviceDeskTickets = tickets,
                    activityLogs = activity
                },
                policy = "Export eligible records before any deletion. Operational tables are not automatically deleted by this endpoint."
            });
        }

        [HttpGet("usage.csv")]
        public async Task<IActionResult> UsageArchiveCsv([FromQuery] int ageDays = 60)
        {
            ageDays = Math.Clamp(ageDays, 30, 3650);
            DateTime cutoff = DateTime.Now.AddDays(-ageDays);

            var rows = await _context.PcUsageHistory
                .AsNoTracking()
                .Where(s => s.LogoutTime.HasValue && s.LogoutTime < cutoff)
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
                $"smartlab-archive-usage-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        private static string Escape(string? value)
        {
            return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        }
    }
}
