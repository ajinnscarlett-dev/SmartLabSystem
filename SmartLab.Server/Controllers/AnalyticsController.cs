using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnalyticsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-30);
            DateTime end = to?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

            var sessions = _context.PcUsageHistory
                .AsNoTracking()
                .Where(s => s.LoginTime >= start && s.LoginTime < end);

            int totalSessions = await sessions.CountAsync();

            double averageDurationSeconds = await sessions
                .Where(s => s.DurationSeconds.HasValue)
                .Select(s => (double?)s.DurationSeconds)
                .AverageAsync() ?? 0;

            var topPc = await sessions
                .GroupBy(s => new
                {
                    s.PCId,
                    PCNumber = s.PC != null ? s.PC.PCNumber : null
                })
                .Select(g => new
                {
                    g.Key.PCId,
                    g.Key.PCNumber,
                    Sessions = g.Count()
                })
                .OrderByDescending(x => x.Sessions)
                .FirstOrDefaultAsync();

            var topLab = await sessions
                .GroupBy(s => new
                {
                    s.LaboratoryId,
                    LaboratoryName = s.Laboratory != null ? s.Laboratory.LabName : null
                })
                .Select(g => new
                {
                    g.Key.LaboratoryId,
                    g.Key.LaboratoryName,
                    Sessions = g.Count()
                })
                .OrderByDescending(x => x.Sessions)
                .FirstOrDefaultAsync();

            var peakHour = await sessions
                .GroupBy(s => s.LoginTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Sessions = g.Count()
                })
                .OrderByDescending(x => x.Sessions)
                .FirstOrDefaultAsync();

            int offlineEvents = await _context.ActivityLogs
                .AsNoTracking()
                .CountAsync(a =>
                    a.CreatedAt >= start &&
                    a.CreatedAt < end &&
                    (a.Action == "PC Offline" ||
                     a.Action == "PC Status Changed" && a.Details.Contains("Offline")));

            int maintenanceEvents = await _context.MaintenanceRecords
                .AsNoTracking()
                .CountAsync(m => m.StartedAt >= start && m.StartedAt < end);

            var serviceDeskByStatus = await _context.ServiceDeskTickets
                .AsNoTracking()
                .Where(t => t.CreatedAt >= start && t.CreatedAt < end)
                .GroupBy(t => t.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var currentPcCounts = await _context.PCs
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Available = g.Count(p => p.Status == "Available"),
                    Occupied = g.Count(p => p.Status == "Occupied"),
                    Offline = g.Count(p => p.Status == "Offline"),
                    Maintenance = g.Count(p => p.Status == "Maintenance"),
                    Total = g.Count()
                })
                .FirstOrDefaultAsync();

            double utilization =
                currentPcCounts == null || currentPcCounts.Total == 0
                    ? 0
                    : Math.Round(
                        currentPcCounts.Occupied * 100d / currentPcCounts.Total,
                        2);

            return Ok(new
            {
                period = new { from = start, to = end.AddDays(-1) },
                totalSessions,
                averageSessionDurationSeconds = Math.Round(averageDurationSeconds, 2),
                mostUsedPc = topPc,
                mostUsedLaboratory = topLab,
                peakUsageHour = peakHour,
                offlineFrequency = offlineEvents,
                maintenanceFrequency = maintenanceEvents,
                serviceDeskVolume = serviceDeskByStatus.Sum(x => x.Count),
                serviceDeskByStatus,
                currentUtilizationPercent = utilization,
                currentPcCounts
            });
        }
    }
}
