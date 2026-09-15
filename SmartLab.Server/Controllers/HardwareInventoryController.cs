using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Teacher")]
    public class HardwareInventoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HardwareInventoryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var query = _context.HardwareInventories
                .AsNoTracking()
                .AsQueryable();

            if (User.IsInRole("Teacher"))
            {
                if (!int.TryParse(
                        User.FindFirstValue(ClaimTypes.NameIdentifier),
                        out int teacherId))
                {
                    return Unauthorized();
                }

                var authorizedLabs = await _context.TeacherLaboratoryAuthorizations
                    .AsNoTracking()
                    .Where(a => a.TeacherUserId == teacherId)
                    .Select(a => a.LaboratoryId)
                    .ToListAsync();

                query = query.Where(h =>
                    h.PC != null &&
                    h.PC.LaboratoryId.HasValue &&
                    authorizedLabs.Contains(h.PC.LaboratoryId.Value));
            }

            var inventory = await query
                .OrderBy(h => h.PCId)
                .Select(h => new
                {
                    h.HardwareInventoryId,
                    h.PCId,
                    PCNumber = h.PC != null ? h.PC.PCNumber : null,
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

            return Ok(inventory);
        }

        [HttpGet("{pcId:int}")]
        public async Task<IActionResult> GetOne(int pcId)
        {
            var item = await _context.HardwareInventories
                .AsNoTracking()
                .Include(h => h.PC)
                .FirstOrDefaultAsync(h => h.PCId == pcId);

            if (item == null)
                return NotFound(new { message = "Hardware inventory not found." });

            if (User.IsInRole("Teacher"))
            {
                if (!int.TryParse(
                        User.FindFirstValue(ClaimTypes.NameIdentifier),
                        out int teacherId))
                    return Unauthorized();

                bool authorized = item.PC?.LaboratoryId.HasValue == true &&
                    await _context.TeacherLaboratoryAuthorizations.AnyAsync(a =>
                        a.TeacherUserId == teacherId &&
                        a.LaboratoryId == item.PC.LaboratoryId.Value);

                if (!authorized)
                    return Forbid();
            }

            return Ok(new
            {
                item.HardwareInventoryId,
                item.PCId,
                PCNumber = item.PC?.PCNumber,
                LaboratoryId = item.PC?.LaboratoryId,
                item.Cpu,
                item.Ram,
                item.Storage,
                item.Gpu,
                item.OperatingSystem,
                item.MACAddress,
                item.IPAddress,
                item.LastAuditedAt
            });
        }

        [HttpPut("{pcId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Upsert(
            int pcId,
            [FromBody] HardwareInventoryRequest request)
        {
            var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == pcId);
            if (pc == null)
                return NotFound(new { message = "PC not found." });

            var item = await _context.HardwareInventories
                .FirstOrDefaultAsync(h => h.PCId == pcId);

            if (item == null)
            {
                item = new HardwareInventory { PCId = pcId };
                _context.HardwareInventories.Add(item);
            }

            item.Cpu = request.Cpu?.Trim();
            item.Ram = request.Ram?.Trim();
            item.Storage = request.Storage?.Trim();
            item.Gpu = request.Gpu?.Trim();
            item.OperatingSystem = request.OperatingSystem?.Trim();
            item.MACAddress = pc.MACAddress;
            item.IPAddress = pc.IPAddress;
            item.LastAuditedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(item);
        }
    }

    public class HardwareInventoryRequest
    {
        public string? Cpu { get; set; }
        public string? Ram { get; set; }
        public string? Storage { get; set; }
        public string? Gpu { get; set; }
        public string? OperatingSystem { get; set; }
    }
}
