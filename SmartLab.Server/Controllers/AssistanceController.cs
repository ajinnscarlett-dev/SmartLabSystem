using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AssistanceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AssistanceController(AppDbContext context)
        {
            _context = context;
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        [HttpGet("mine")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMine()
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var requests = await _context.AssistanceRequests
                .AsNoTracking()
                .Where(a => a.StudentUserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .Select(a => new
                {
                    a.AssistanceRequestId,
                    a.PCId,
                    a.LaboratoryId,
                    a.Category,
                    a.Description,
                    a.Status,
                    a.CreatedAt,
                    a.AcknowledgedAt,
                    a.StartedAt,
                    a.ResolvedAt,
                    a.ClosedAt,
                    a.ResolutionNotes
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Create(
            [FromBody] AssistanceCreateRequest request)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            if (request.PCId <= 0 ||
                string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new
                {
                    message = "PC and assistance description are required."
                });
            }

            PC? pc = await _context.PCs
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.PCId == request.PCId &&
                    p.CurrentUserId == userId &&
                    p.Status == "Occupied");

            if (pc == null)
            {
                return Conflict(new
                {
                    message = "The selected PC is not currently assigned to your student session."
                });
            }

            bool hasOpenRequest = await _context.AssistanceRequests
                .AnyAsync(a =>
                    a.StudentUserId == userId &&
                    a.PCId == pc.PCId &&
                    a.Status != "Resolved" &&
                    a.Status != "Closed");

            if (hasOpenRequest)
            {
                return Conflict(new
                {
                    message = "You already have an active assistance request for this PC."
                });
            }

            var assistance = new AssistanceRequest
            {
                StudentUserId = userId,
                PCId = pc.PCId,
                LaboratoryId = pc.LaboratoryId,
                Category = string.IsNullOrWhiteSpace(request.Category)
                    ? "General"
                    : request.Category.Trim(),
                Description = request.Description.Trim(),
                Status = "Open",
                CreatedAt = DateTime.Now
            };

            _context.AssistanceRequests.Add(assistance);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMine),
                new { id = assistance.AssistanceRequestId },
                assistance);
        }

        [HttpGet("queue")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> GetQueue(
            [FromQuery] int? laboratoryId = null,
            [FromQuery] string? status = null)
        {
            var query = _context.AssistanceRequests
                .AsNoTracking()
                .Include(a => a.StudentUser)
                .Include(a => a.PC)
                .Include(a => a.Laboratory)
                .AsQueryable();

            if (laboratoryId.HasValue)
            {
                query = query.Where(a => a.LaboratoryId == laboratoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }
            else
            {
                query = query.Where(a => a.Status != "Closed");
            }

            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetUserId(out int teacherId))
                    return Unauthorized();

                var authorizedLabs = await _context.TeacherLaboratoryAuthorizations
                    .Where(a => a.TeacherUserId == teacherId)
                    .Select(a => a.LaboratoryId)
                    .ToListAsync();

                query = query.Where(a =>
                    a.LaboratoryId.HasValue &&
                    authorizedLabs.Contains(a.LaboratoryId.Value));
            }

            var requests = await query
                .OrderBy(a => a.Status == "Open" ? 0 : 1)
                .ThenBy(a => a.CreatedAt)
                .Select(a => new
                {
                    a.AssistanceRequestId,
                    a.StudentUserId,
                    StudentUsername = a.StudentUser != null ? a.StudentUser.Username : null,
                    a.PCId,
                    PCNumber = a.PC != null ? a.PC.PCNumber : null,
                    a.LaboratoryId,
                    LaboratoryName = a.Laboratory != null ? a.Laboratory.LabName : null,
                    a.Category,
                    a.Description,
                    a.Status,
                    a.CreatedAt,
                    a.AcknowledgedAt,
                    a.StartedAt,
                    a.ResolvedAt,
                    a.ClosedAt,
                    a.ResolutionNotes
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateStatus(
            long id,
            [FromBody] AssistanceStatusUpdateRequest request)
        {
            string status = request.Status?.Trim() ?? string.Empty;

            string[] allowedStatuses =
            {
                "Open",
                "Acknowledged",
                "In Progress",
                "Resolved",
                "Closed"
            };

            if (!allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid assistance status." });
            }

            var assistance = await _context.AssistanceRequests
                .FirstOrDefaultAsync(a => a.AssistanceRequestId == id);

            if (assistance == null)
            {
                return NotFound(new { message = "Assistance request not found." });
            }

            string role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetUserId(out int teacherId))
                    return Unauthorized();

                if (!assistance.LaboratoryId.HasValue ||
                    !await _context.TeacherLaboratoryAuthorizations.AnyAsync(a =>
                        a.TeacherUserId == teacherId &&
                        a.LaboratoryId == assistance.LaboratoryId.Value))
                {
                    return Forbid();
                }
            }

            DateTime now = DateTime.Now;
            assistance.Status = allowedStatuses.First(v =>
                v.Equals(status, StringComparison.OrdinalIgnoreCase));

            switch (assistance.Status)
            {
                case "Acknowledged":
                    assistance.AcknowledgedAt ??= now;
                    break;
                case "In Progress":
                    assistance.AcknowledgedAt ??= now;
                    assistance.StartedAt ??= now;
                    break;
                case "Resolved":
                    assistance.AcknowledgedAt ??= now;
                    assistance.StartedAt ??= now;
                    assistance.ResolvedAt ??= now;
                    assistance.ResolvedByUserId = TryGetUserId(out int resolverId)
                        ? resolverId
                        : null;
                    if (!string.IsNullOrWhiteSpace(request.ResolutionNotes))
                    {
                        assistance.ResolutionNotes = request.ResolutionNotes.Trim();
                    }
                    break;
                case "Closed":
                    assistance.ClosedAt ??= now;
                    assistance.ResolvedAt ??= now;
                    assistance.ResolvedByUserId ??= TryGetUserId(out int closerId)
                        ? closerId
                        : null;
                    if (!string.IsNullOrWhiteSpace(request.ResolutionNotes))
                    {
                        assistance.ResolutionNotes = request.ResolutionNotes.Trim();
                    }
                    break;
            }

            await _context.SaveChangesAsync();
            return Ok(assistance);
        }
    }

    public class AssistanceCreateRequest
    {
        public int PCId { get; set; }
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
    }

    public class AssistanceStatusUpdateRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? ResolutionNotes { get; set; }
    }
}
