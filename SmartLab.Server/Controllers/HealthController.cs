using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HealthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            bool databaseHealthy;
            string? databaseError = null;

            try
            {
                databaseHealthy = await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                databaseHealthy = false;
                databaseError = ex.Message;
            }

            var counts = await _context.PCs
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Available = g.Count(p => p.Status == "Available"),
                    Occupied = g.Count(p => p.Status == "Occupied"),
                    Offline = g.Count(p => p.Status == "Offline"),
                    Maintenance = g.Count(p => p.Status == "Maintenance")
                })
                .FirstOrDefaultAsync();

            int available = counts?.Available ?? 0;
            int occupied = counts?.Occupied ?? 0;
            int offline = counts?.Offline ?? 0;
            int maintenance = counts?.Maintenance ?? 0;
            int totalPcs = available + occupied + offline + maintenance;

            return Ok(new
            {
                server = "Online",
                database = databaseHealthy ? "Online" : "Offline",
                databaseConnected = databaseHealthy,
                databaseError,
                available,
                occupied,
                offline,
                maintenance,
                totalPCs = totalPcs,
                checkedAt = DateTime.Now
            });
        }
    }
}
