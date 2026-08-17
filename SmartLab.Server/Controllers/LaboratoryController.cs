using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class LaboratoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LaboratoryController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET ALL LABORATORIES
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetAllLaboratories()
        {
            // Capture the cutoff outside the EF query.
            // This makes the date comparison SQL-translatable.
            DateTime onlineCutoff =
                DateTime.Now.AddSeconds(-30);

            var laboratories =
                await _context.Laboratories
                    .Include(l => l.PCs)
                    .OrderBy(l => l.LaboratoryId)
                    .Select(l => new
                    {
                        l.LaboratoryId,
                        l.LabName,
                        l.Description,

                        PCCount = l.PCs.Count(),

                        OnlineCount = l.PCs.Count(p =>
                            p.IsEnabled &&
                            p.LastSeen.HasValue &&
                            p.LastSeen.Value >= onlineCutoff),

                        OccupiedCount = l.PCs.Count(p =>
                            p.CurrentUserId != null ||
                            p.Status == "Occupied" ||
                            p.Status == "In Use"),

                        MaintenanceCount = l.PCs.Count(p =>
                            p.Status == "Maintenance")
                    })
                    .ToListAsync();

            return Ok(laboratories);
        }

        // ==========================================
        // GET ONE LABORATORY
        // ==========================================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetLaboratory(
            int id)
        {
            DateTime onlineCutoff =
                DateTime.Now.AddSeconds(-30);

            var laboratory =
                await _context.Laboratories
                    .Include(l => l.PCs)
                    .Where(l =>
                        l.LaboratoryId == id)
                    .Select(l => new
                    {
                        l.LaboratoryId,
                        l.LabName,
                        l.Description,

                        PCCount = l.PCs.Count(),

                        OnlineCount = l.PCs.Count(p =>
                            p.IsEnabled &&
                            p.LastSeen.HasValue &&
                            p.LastSeen.Value >= onlineCutoff),

                        OccupiedCount = l.PCs.Count(p =>
                            p.CurrentUserId != null ||
                            p.Status == "Occupied" ||
                            p.Status == "In Use"),

                        MaintenanceCount = l.PCs.Count(p =>
                            p.Status == "Maintenance")
                    })
                    .FirstOrDefaultAsync();

            if (laboratory == null)
            {
                return NotFound(new
                {
                    message = "Laboratory not found."
                });
            }

            return Ok(laboratory);
        }

        // ==========================================
        // GET LABORATORY PCS
        // ==========================================

        [HttpGet("{id}/pcs")]
        public async Task<IActionResult> GetLaboratoryPCs(
            int id)
        {
            var laboratoryExists =
                await _context.Laboratories
                    .AnyAsync(l =>
                        l.LaboratoryId == id);

            if (!laboratoryExists)
            {
                return NotFound(new
                {
                    message = "Laboratory not found."
                });
            }

            var pcs =
                await _context.PCs
                    .Include(p => p.CurrentUser)
                    .Include(p => p.Laboratory)
                    .Where(p =>
                        p.LaboratoryId == id)
                    .OrderBy(p => p.PCNumber)
                    .Select(p => new
                    {
                        p.PCId,
                        p.PCNumber,
                        p.Status,
                        p.CurrentUserId,

                        Username = p.CurrentUser != null
                            ? p.CurrentUser.Username
                            : null,

                        p.LaboratoryId,

                        LaboratoryName =
                            p.Laboratory != null
                                ? p.Laboratory.LabName
                                : null,

                        p.MACAddress,
                        p.IPAddress,
                        p.LastSeen,
                        p.IsEnabled,
                        p.MaintenanceReason,
                        p.MaintenanceStarted
                    })
                    .ToListAsync();

            return Ok(pcs);
        }
    }
}
