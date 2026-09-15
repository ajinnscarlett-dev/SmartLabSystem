using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Teacher")]
    public class UsageHistoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsageHistoryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            DateTime? from = null,
            DateTime? to = null,
            int? laboratoryId = null,
            int? pcId = null,
            int? userId = null)
        {
            DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-30);
            DateTime end = to?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

            var query = _context.PcUsageHistory
                .AsNoTracking()
                .Where(s => s.LoginTime >= start && s.LoginTime < end);

            if (laboratoryId.HasValue)
                query = query.Where(s => s.LaboratoryId == laboratoryId.Value);
            if (pcId.HasValue)
                query = query.Where(s => s.PCId == pcId.Value);
            if (userId.HasValue)
                query = query.Where(s => s.UserId == userId.Value);

            var result = await query
                .OrderByDescending(s => s.LoginTime)
                .Select(s => new
                {
                    s.SessionId,
                    s.PCId,
                    PCNumber = s.PC != null ? s.PC.PCNumber : null,
                    s.UserId,
                    Username = s.User != null ? s.User.Username : null,
                    s.LaboratoryId,
                    LaboratoryName = s.Laboratory != null ? s.Laboratory.LabName : null,
                    s.LoginTime,
                    s.LogoutTime,
                    s.DurationSeconds,
                    s.EndReason
                })
                .ToListAsync();

            return Ok(result);
        }
    }
}
