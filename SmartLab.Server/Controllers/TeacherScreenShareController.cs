using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeacherScreenShareController : ControllerBase
    {
        private static readonly TimeSpan ShareMaxAge = TimeSpan.FromSeconds(30);
        private readonly AppDbContext _context;
        private readonly TeacherScheduleService _scheduleService;

        private sealed class TeacherShareState
        {
            public int LaboratoryId { get; init; }
            public int TeacherUserId { get; init; }
            public byte[]? LatestFrame { get; set; }
            public long Version { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private static readonly ConcurrentDictionary<int, TeacherShareState> ActiveShares = new();
        private static long _frameVersion;

        public TeacherScreenShareController(AppDbContext context, TeacherScheduleService scheduleService)
        {
            _context = context;
            _scheduleService = scheduleService;
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{laboratoryId}/start")]
        public async Task<IActionResult> Start(int laboratoryId)
        {
            CleanupStaleShares();
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();
            if (!await CanManageLaboratoryAsync(userId, laboratoryId)) return Forbid();
            var laboratory = await _context.Laboratories.AsNoTracking().FirstOrDefaultAsync(l => l.LaboratoryId == laboratoryId);
            if (laboratory == null) return NotFound(new { message = "Laboratory not found." });

            ActiveShares[laboratoryId] = new TeacherShareState { LaboratoryId = laboratoryId, TeacherUserId = userId, UpdatedAt = DateTime.Now };
            await LogActivityAsync(userId, null, "Teacher Screen Share Started", $"Teacher screen sharing started for COMLAB {laboratory.LabName}.");
            return Ok(new { message = "Teacher screen sharing started.", laboratoryId, laboratoryName = laboratory.LabName, sharing = true });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{laboratoryId}/stop")]
        public async Task<IActionResult> Stop(int laboratoryId)
        {
            CleanupStaleShares();
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();
            if (!await CanManageLaboratoryAsync(userId, laboratoryId)) return Forbid();
            if (ActiveShares.TryGetValue(laboratoryId, out TeacherShareState? share) && share.TeacherUserId != userId && !User.IsInRole("Admin")) return Forbid();

            ActiveShares.TryRemove(laboratoryId, out _);
            string labName = await _context.Laboratories.AsNoTracking().Where(l => l.LaboratoryId == laboratoryId).Select(l => l.LabName).FirstOrDefaultAsync() ?? $"Laboratory {laboratoryId}";
            await LogActivityAsync(userId, null, "Teacher Screen Share Stopped", $"Teacher screen sharing stopped for COMLAB {labName}.");
            return Ok(new { message = "Teacher screen sharing stopped.", laboratoryId, sharing = false });
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("{laboratoryId}/frame")]
        public async Task<IActionResult> UploadFrame(int laboratoryId)
        {
            CleanupStaleShares();
            if (!TryGetCurrentUserId(out int teacherUserId)) return Unauthorized();
            if (!await CanManageLaboratoryAsync(teacherUserId, laboratoryId)) return Forbid();
            if (!ActiveShares.TryGetValue(laboratoryId, out TeacherShareState? share)) return StatusCode(StatusCodes.Status409Conflict, new { message = "Teacher screen sharing is not active." });
            if (share.TeacherUserId != teacherUserId) return Forbid();

            const int maxFrameBytes = 2 * 1024 * 1024;
            if (Request.ContentLength.HasValue && Request.ContentLength.Value > maxFrameBytes) return BadRequest(new { message = "Teacher screen frame is too large." });
            using MemoryStream stream = new();
            await Request.Body.CopyToAsync(stream, HttpContext.RequestAborted);
            if (stream.Length == 0) return BadRequest(new { message = "Teacher screen frame is empty." });
            if (stream.Length > maxFrameBytes) return BadRequest(new { message = "Teacher screen frame is too large." });

            long version = Interlocked.Increment(ref _frameVersion);
            share.LatestFrame = stream.ToArray();
            share.Version = version;
            share.UpdatedAt = DateTime.Now;
            return Ok(new { message = "Teacher screen frame uploaded.", laboratoryId, version, size = share.LatestFrame.Length });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{laboratoryId}/status")]
        public async Task<IActionResult> Status(int laboratoryId)
        {
            CleanupStaleShares();
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();
            if (!await CanManageLaboratoryAsync(userId, laboratoryId)) return Forbid();
            if (!ActiveShares.TryGetValue(laboratoryId, out TeacherShareState? share)) return Ok(new { laboratoryId, sharing = false });
            return Ok(new { laboratoryId, sharing = true, version = share.Version, updatedAt = share.UpdatedAt });
        }

        [Authorize(Roles = "Student")]
        [HttpGet("frame")]
        public async Task<IActionResult> GetStudentFrame()
        {
            CleanupStaleShares();
            if (!TryGetCurrentUserId(out int studentUserId)) return Unauthorized();
            int? laboratoryId = await _context.PCs.AsNoTracking().Where(p => p.CurrentUserId == studentUserId && p.IsEnabled && p.LaboratoryId.HasValue).Select(p => p.LaboratoryId).FirstOrDefaultAsync();
            if (!laboratoryId.HasValue) return NotFound(new { message = "The student is not assigned to a laboratory PC." });
            if (!ActiveShares.TryGetValue(laboratoryId.Value, out TeacherShareState? share) || share.LatestFrame == null || share.LatestFrame.Length == 0) return NoContent();

            Response.Headers["X-SmartLab-Teacher-Frame-Version"] = share.Version.ToString();
            Response.Headers["X-SmartLab-Teacher-Frame-Time"] = share.UpdatedAt.ToString("O");
            return File(share.LatestFrame, "image/jpeg");
        }

        private static void CleanupStaleShares()
        {
            DateTime cutoff = DateTime.Now.Subtract(ShareMaxAge);
            foreach (var pair in ActiveShares)
            {
                if (pair.Value.UpdatedAt < cutoff) ActiveShares.TryRemove(pair.Key, out _);
            }
        }

        private async Task<bool> CanManageLaboratoryAsync(int userId, int laboratoryId)
        {
            if (User.IsInRole("Admin")) return true;
            if (!User.IsInRole("Teacher")) return false;
            return await _scheduleService.IsTeacherScheduledAsync(userId, laboratoryId);
        }

        private bool TryGetCurrentUserId(out int userId) => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

        private async Task LogActivityAsync(int? userId, int? pcId, string action, string details)
        {
            _context.ActivityLogs.Add(new ActivityLog { UserId = userId, PCId = pcId, Action = action, Details = details, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
        }
    }
}
