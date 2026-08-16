using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivityLogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActivityLogController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET ALL ACTIVITY LOGS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetActivityLogs()
        {
            var logs = await _context.ActivityLogs
                .Include(a => a.User)
                .Include(a => a.PC)
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .Select(a => new
                {
                    activityLogId = a.ActivityLogId,
                    pcId = a.PCId,
                    pcNumber = a.PC != null
                        ? a.PC.PCNumber
                        : null,

                    userId = a.UserId,
                    username = a.User != null
                        ? a.User.Username
                        : null,

                    action = a.Action,
                    details = a.Details,
                    createdAt = a.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}