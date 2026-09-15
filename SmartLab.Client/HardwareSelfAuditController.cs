using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [Route("api/HardwareInventory")]
    [ApiController]
    [Authorize(Roles = "Student")]
    public class HardwareSelfAuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HardwareSelfAuditController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPut("{pcId:int}/self")]
        public async Task<IActionResult> UpsertSelf(
            int pcId,
            [FromBody] HardwareInventoryRequest request)
        {
            if (!int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out int userId))
            {
                return Unauthorized();
            }

            PC? pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == pcId &&
                    p.CurrentUserId == userId);

            if (pc == null)
            {
                return Forbid();
            }

            HardwareInventory? item = await _context.HardwareInventories
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

            return Ok(new
            {
                message = "Hardware inventory updated.",
                pcId = pc.PCId,
                lastAuditedAt = item.LastAuditedAt
            });
        }
    }
}
