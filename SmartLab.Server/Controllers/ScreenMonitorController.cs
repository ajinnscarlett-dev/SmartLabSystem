using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScreenMonitorController : ControllerBase
    {
        private const int MaxFrameBytes = 4 * 1024 * 1024;
        private static readonly TimeSpan FrameMaxAge = TimeSpan.FromSeconds(30);

        private readonly AppDbContext _context;
        private readonly TeacherScheduleService _scheduleService;

        private sealed class ScreenFrame
        {
            public byte[] Image { get; }
            public long Version { get; }
            public DateTime UpdatedAt { get; }
            public ScreenFrame(byte[] image, long version, DateTime updatedAt) { Image = image; Version = version; UpdatedAt = updatedAt; }
        }

        private static readonly ConcurrentDictionary<int, ScreenFrame> LatestFrames = new();
        private static readonly ConcurrentDictionary<int, bool> MonitoringStates = new();
        private static long _globalFrameVersion;

        public ScreenMonitorController(AppDbContext context, TeacherScheduleService scheduleService)
        {
            _context = context;
            _scheduleService = scheduleService;
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpGet("{pcId}/status")]
        public async Task<IActionResult> GetMonitoringStatus(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanAccessPcAsync(pcId)) return Forbid();
            bool enabled = MonitoringStates.TryGetValue(pcId, out bool state) && state;
            return Ok(new { pcId, monitoring = enabled });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/start")]
        public async Task<IActionResult> StartMonitoring(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanStaffAccessPcAsync(pcId)) return Forbid();
            MonitoringStates[pcId] = true;
            return Ok(new { message = "Screen monitoring started.", pcId, monitoring = true });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/stop")]
        public async Task<IActionResult> StopMonitoring(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanStaffAccessPcAsync(pcId)) return Forbid();
            MonitoringStates[pcId] = false;
            LatestFrames.TryRemove(pcId, out _);
            return Ok(new { message = "Screen monitoring stopped.", pcId, monitoring = false });
        }

        [Authorize(Roles = "Student")]
        [HttpPost("{pcId}")]
        public async Task<IActionResult> UploadScreen(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanStudentAccessPcAsync(pcId)) return Forbid();
            if (!MonitoringStates.TryGetValue(pcId, out bool enabled) || !enabled)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Screen monitoring is not enabled." });

            if (Request.ContentLength.HasValue && (Request.ContentLength.Value <= 0 || Request.ContentLength.Value > MaxFrameBytes))
                return BadRequest(new { message = $"Screen image must be between 1 byte and {MaxFrameBytes:N0} bytes." });

            using MemoryStream stream = new();
            await Request.Body.CopyToAsync(stream, HttpContext.RequestAborted);
            if (stream.Length == 0) return BadRequest(new { message = "Screen image is empty." });
            if (stream.Length > MaxFrameBytes) return BadRequest(new { message = $"Screen image exceeds the {MaxFrameBytes:N0}-byte limit." });

            byte[] image = stream.ToArray();
            long version = Interlocked.Increment(ref _globalFrameVersion);
            LatestFrames[pcId] = new ScreenFrame(image, version, DateTime.Now);
            return Ok(new { message = "Screen uploaded successfully.", pcId, version, size = image.Length });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{pcId}/meta")]
        public async Task<IActionResult> GetFrameMetadata(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanStaffAccessPcAsync(pcId)) return Forbid();
            if (!LatestFrames.TryGetValue(pcId, out ScreenFrame? frame)) return NotFound(new { message = "No current screen image is available for this PC." });
            TouchRemoteViewerLease(pcId);
            return Ok(new { pcId, version = frame.Version, updatedAt = frame.UpdatedAt });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{pcId}")]
        public async Task<IActionResult> GetScreen(int pcId, [FromQuery] long? version = null)
        {
            CleanupStaleFrames();
            if (!await CanStaffAccessPcAsync(pcId)) return Forbid();
            if (!LatestFrames.TryGetValue(pcId, out ScreenFrame? frame)) return NotFound(new { message = "No current screen image is available for this PC." });

            // Refresh the remote-control lease even when there is no newer frame.
            TouchRemoteViewerLease(pcId);
            if (version.HasValue && version.Value >= frame.Version) return NoContent();

            Response.Headers["X-SmartLab-Frame-Version"] = frame.Version.ToString();
            Response.Headers["X-SmartLab-Frame-Time"] = frame.UpdatedAt.ToString("O");
            return File(frame.Image, "image/jpeg");
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpDelete("{pcId}")]
        public async Task<IActionResult> RemoveScreen(int pcId)
        {
            CleanupStaleFrames();
            if (!await CanAccessPcAsync(pcId)) return Forbid();
            LatestFrames.TryRemove(pcId, out _);
            if (User.IsInRole("Admin") || User.IsInRole("Teacher"))
            {
                MonitoringStates[pcId] = false;
                RemoteControlSessionTracker.Remove(pcId);
            }
            return Ok(new { message = "Screen monitoring data removed.", pcId });
        }

        private void TouchRemoteViewerLease(int pcId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return;

            RemoteControlSessionTracker.Touch(pcId, userId);
        }

        private static void CleanupStaleFrames()
        {
            DateTime cutoff = DateTime.Now.Subtract(FrameMaxAge);
            foreach (var pair in LatestFrames)
            {
                if (pair.Value.UpdatedAt < cutoff)
                    LatestFrames.TryRemove(pair.Key, out _);
            }
        }

        private async Task<bool> CanAccessPcAsync(int pcId)
        {
            if (User.IsInRole("Admin")) return true;
            if (User.IsInRole("Teacher")) return await CanTeacherAccessPcAsync(pcId);
            return await CanStudentAccessPcAsync(pcId);
        }

        private async Task<bool> CanStaffAccessPcAsync(int pcId)
        {
            if (User.IsInRole("Admin")) return true;
            if (User.IsInRole("Teacher")) return await CanTeacherAccessPcAsync(pcId);
            return false;
        }

        private async Task<bool> CanTeacherAccessPcAsync(int pcId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int teacherId)) return false;

            PC? pc = await _context.PCs.AsNoTracking().FirstOrDefaultAsync(p => p.PCId == pcId);
            if (pc == null || !pc.LaboratoryId.HasValue)
                return false;

            return await _scheduleService.IsTeacherScheduledAsync(
                teacherId,
                pc.LaboratoryId.Value);
        }

        private async Task<bool> CanStudentAccessPcAsync(int pcId)
        {
            if (!User.IsInRole("Student")) return false;
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int authenticatedUserId)) return false;
            return await _context.PCs.AsNoTracking().AnyAsync(pc => pc.PCId == pcId && pc.CurrentUserId == authenticatedUserId && pc.IsEnabled);
        }
    }
}
