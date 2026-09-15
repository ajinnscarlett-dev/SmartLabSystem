using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class MaintenanceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MaintenanceController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int? pcId = null,
            [FromQuery] bool openOnly = false)
        {
            var query = _context.MaintenanceRecords
                .AsNoTracking()
                .Include(m => m.PC)
                .Include(m => m.TechnicianUser)
                .AsQueryable();

            if (pcId.HasValue)
                query = query.Where(m => m.PCId == pcId.Value);

            if (openOnly)
                query = query.Where(m => m.EndedAt == null);

            var records = await query
                .OrderByDescending(m => m.StartedAt)
                .Select(m => new
                {
                    m.MaintenanceRecordId,
                    m.PCId,
                    PCNumber = m.PC != null ? m.PC.PCNumber : null,
                    m.Reason,
                    m.StartedAt,
                    m.EndedAt,
                    m.TechnicianUserId,
                    Technician = m.TechnicianUser != null ? m.TechnicianUser.Username : null,
                    m.Notes
                })
                .ToListAsync();

            return Ok(records);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(
            long id,
            [FromBody] MaintenanceUpdateRequest request)
        {
            var record = await _context.MaintenanceRecords
                .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

            if (record == null)
                return NotFound(new { message = "Maintenance record not found." });

            if (request.TechnicianUserId.HasValue)
            {
                bool isValidTechnician = await _context.Users.AnyAsync(u =>
                    u.UserId == request.TechnicianUserId.Value &&
                    (u.Role == "Admin" || u.Role == "Teacher"));

                if (!isValidTechnician)
                {
                    return BadRequest(new
                    {
                        message = "Selected technician account is not valid."
                    });
                }
            }

            record.TechnicianUserId = request.TechnicianUserId;
            record.Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Maintenance record updated.",
                record
            });
        }
    }

    public class MaintenanceUpdateRequest
    {
        public int? TechnicianUserId { get; set; }
        public string? Notes { get; set; }
    }
}
