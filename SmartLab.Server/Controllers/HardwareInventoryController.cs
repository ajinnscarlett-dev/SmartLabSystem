using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var inventory = await _context.HardwareInventories
                .AsNoTracking()
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

        [HttpPut("{pcId:int}")]
        public async Task<IActionResult> Upsert(int pcId, [FromBody] HardwareInventoryRequest request)
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
