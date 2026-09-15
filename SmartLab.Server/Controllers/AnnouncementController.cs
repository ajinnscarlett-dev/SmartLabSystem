using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnnouncementController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnnouncementController(AppDbContext context)
        {
            _context = context;
        }

        private string CurrentRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            string role = CurrentRole;
            DateTime now = DateTime.Now;

            var announcements = await _context.Announcements
                .AsNoTracking()
                .Where(a =>
                    a.IsActive &&
                    (!a.ExpiresAt.HasValue || a.ExpiresAt.Value > now) &&
                    (a.TargetRole == "All" ||
                     a.TargetRole == role))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(announcements);
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllAnnouncements()
        {
            var announcements = await _context.Announcements
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(announcements);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAnnouncement(int id)
        {
            var announcement = await _context.Announcements
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new { message = "Announcement not found." });
            }

            if (!string.Equals(CurrentRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(announcement.TargetRole, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(announcement.TargetRole, CurrentRole, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return Ok(announcement);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAnnouncement(
            [FromBody] AnnouncementCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Announcement title is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Announcement message is required." });
            }

            if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.Now)
            {
                return BadRequest(new { message = "Expiration date must be in the future." });
            }

            string targetRole = request.TargetRole?.Trim() ?? "All";

            if (!new[] { "All", "Student", "Teacher", "Admin" }
                .Contains(targetRole, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid target role." });
            }

            int? postedByUserId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                postedByUserId = currentUserId;
            }

            var announcement = new Announcement
            {
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                PostedByUserId = postedByUserId,
                CreatedAt = DateTime.Now,
                IsActive = true,
                ExpiresAt = request.ExpiresAt,
                TargetRole = new[] { "All", "Student", "Teacher", "Admin" }
                    .First(v => v.Equals(targetRole, StringComparison.OrdinalIgnoreCase))
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Announcement created successfully.",
                announcement
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditAnnouncement(
            int id,
            [FromBody] AnnouncementEditRequest request)
        {
            var announcement = await _context.Announcements
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new { message = "Announcement not found." });
            }

            if (string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Title and message are required." });
            }

            if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.Now)
            {
                return BadRequest(new { message = "Expiration date must be in the future." });
            }

            if (!new[] { "All", "Student", "Teacher", "Admin" }
                .Contains(request.TargetRole, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid target role." });
            }

            announcement.Title = request.Title.Trim();
            announcement.Message = request.Message.Trim();
            announcement.ExpiresAt = request.ExpiresAt;
            announcement.TargetRole = new[] { "All", "Student", "Teacher", "Admin" }
                .First(v => v.Equals(request.TargetRole, StringComparison.OrdinalIgnoreCase));

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Announcement updated successfully.",
                announcement
            });
        }

        [HttpPut("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeactivateAnnouncement(int id)
        {
            var announcement = await _context.Announcements
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new { message = "Announcement not found." });
            }

            announcement.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Announcement deactivated successfully.",
                announcement
            });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.Announcements
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new { message = "Announcement not found." });
            }

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Announcement deleted successfully." });
        }
    }

    public class AnnouncementCreateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? PostedByUserId { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string TargetRole { get; set; } = "All";
    }

    public class AnnouncementEditRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public string TargetRole { get; set; } = "All";
    }
}
