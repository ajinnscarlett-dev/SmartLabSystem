using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnnouncementController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnnouncementController(AppDbContext context)
        {
            _context = context;
        }


        // ==========================================
        // GET ALL ACTIVE ANNOUNCEMENTS
        //
        // GET:
        // api/Announcement
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            var now = DateTime.Now;

            var announcements =
                await _context.Announcements
                    .Where(a =>
                        a.IsActive &&
                        (!a.ExpiresAt.HasValue ||
                         a.ExpiresAt.Value > now))
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

            return Ok(announcements);
        }


        // ==========================================
        // GET ANNOUNCEMENT BY ID
        //
        // GET:
        // api/Announcement/{id}
        // ==========================================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAnnouncement(
            int id)
        {
            var announcement =
                await _context.Announcements
                    .FirstOrDefaultAsync(
                        a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new
                {
                    message = "Announcement not found."
                });
            }

            return Ok(announcement);
        }


        // ==========================================
        // CREATE ANNOUNCEMENT
        //
        // POST:
        // api/Announcement
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CreateAnnouncement(
            [FromBody] AnnouncementCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new
                {
                    message = "Announcement title is required."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    message = "Announcement message is required."
                });
            }


            // ==========================================
            // VALIDATE EXPIRATION
            // ==========================================

            if (request.ExpiresAt.HasValue &&
                request.ExpiresAt.Value <= DateTime.Now)
            {
                return BadRequest(new
                {
                    message =
                        "Expiration date must be in the future."
                });
            }


            var announcement =
                new Announcement
                {
                    Title = request.Title.Trim(),

                    Message = request.Message.Trim(),

                    PostedByUserId =
                        request.PostedByUserId,

                    CreatedAt =
                        DateTime.Now,

                    IsActive = true,

                    ExpiresAt =
                        request.ExpiresAt
                };


            _context.Announcements.Add(
                announcement);

            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Announcement created successfully.",

                announcement
            });
        }


        // ==========================================
        // DEACTIVATE ANNOUNCEMENT
        //
        // PUT:
        // api/Announcement/{id}/deactivate
        // ==========================================

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivateAnnouncement(
            int id)
        {
            var announcement =
                await _context.Announcements
                    .FirstOrDefaultAsync(
                        a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new
                {
                    message = "Announcement not found."
                });
            }


            announcement.IsActive = false;

            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Announcement deactivated successfully.",

                announcement
            });
        }


        // ==========================================
        // DELETE ANNOUNCEMENT
        //
        // DELETE:
        // api/Announcement/{id}
        // ==========================================

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnnouncement(
            int id)
        {
            var announcement =
                await _context.Announcements
                    .FirstOrDefaultAsync(
                        a => a.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound(new
                {
                    message = "Announcement not found."
                });
            }


            _context.Announcements.Remove(
                announcement);

            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Announcement deleted successfully."
            });
        }
    }


    // ==========================================
    // CREATE ANNOUNCEMENT REQUEST
    // ==========================================

    public class AnnouncementCreateRequest
    {
        public string Title { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public int? PostedByUserId { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}