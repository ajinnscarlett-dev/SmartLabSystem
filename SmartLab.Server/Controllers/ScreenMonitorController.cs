using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScreenMonitorController : ControllerBase
    {
        private readonly AppDbContext _context;

        private sealed class ScreenFrame
        {
            public byte[] Image { get; }
            public long Version { get; }
            public DateTime UpdatedAt { get; }

            public ScreenFrame(byte[] image, long version, DateTime updatedAt)
            {
                Image = image;
                Version = version;
                UpdatedAt = updatedAt;
            }
        }

        private static readonly ConcurrentDictionary<int, ScreenFrame>
            LatestFrames = new();

        private static readonly ConcurrentDictionary<int, bool>
            MonitoringStates = new();

        private static long _globalFrameVersion;

        public ScreenMonitorController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpGet("{pcId}/status")]
        public async Task<IActionResult> GetMonitoringStatus(int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
                return Forbid();

            bool enabled = MonitoringStates.TryGetValue(pcId, out bool state) && state;

            return Ok(new
            {
                pcId,
                monitoring = enabled
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/start")]
        public IActionResult StartMonitoring(int pcId)
        {
            MonitoringStates[pcId] = true;

            return Ok(new
            {
                message = "Screen monitoring started.",
                pcId,
                monitoring = true
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/stop")]
        public IActionResult StopMonitoring(int pcId)
        {
            MonitoringStates[pcId] = false;
            LatestFrames.TryRemove(pcId, out _);

            return Ok(new
            {
                message = "Screen monitoring stopped.",
                pcId,
                monitoring = false
            });
        }

        [Authorize(Roles = "Student")]
        [HttpPost("{pcId}")]
        public async Task<IActionResult> UploadScreen(int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
                return Forbid();

            bool enabled = MonitoringStates.TryGetValue(pcId, out bool state) && state;

            if (!enabled)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Screen monitoring is not enabled." });
            }

            if (Request.ContentLength == 0)
            {
                return BadRequest(new { message = "Screen image is empty." });
            }

            using MemoryStream stream = new MemoryStream();
            await Request.Body.CopyToAsync(stream);

            byte[] image = stream.ToArray();

            if (image.Length == 0)
            {
                return BadRequest(new { message = "Screen image is empty." });
            }

            long version = Interlocked.Increment(ref _globalFrameVersion);

            LatestFrames[pcId] =
                new ScreenFrame(image, version, DateTime.Now);

            return Ok(new
            {
                message = "Screen uploaded successfully.",
                pcId,
                version,
                size = image.Length
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{pcId}/meta")]
        public IActionResult GetFrameMetadata(int pcId)
        {
            if (!LatestFrames.TryGetValue(pcId, out ScreenFrame? frame))
            {
                return NotFound(new
                {
                    message = "No screen image available for this PC."
                });
            }

            return Ok(new
            {
                pcId,
                version = frame.Version,
                updatedAt = frame.UpdatedAt
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{pcId}")]
        public IActionResult GetScreen(int pcId, [FromQuery] long? version = null)
        {
            if (!LatestFrames.TryGetValue(pcId, out ScreenFrame? frame))
            {
                return NotFound(new
                {
                    message = "No screen image available for this PC."
                });
            }

            if (version.HasValue && version.Value >= frame.Version)
            {
                return NoContent();
            }

            Response.Headers["X-SmartLab-Frame-Version"] =
                frame.Version.ToString();

            Response.Headers["X-SmartLab-Frame-Time"] =
                frame.UpdatedAt.ToString("O");

            return File(frame.Image, "image/jpeg");
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpDelete("{pcId}")]
        public async Task<IActionResult> RemoveScreen(int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
                return Forbid();

            LatestFrames.TryRemove(pcId, out _);

            if (User.IsInRole("Admin") || User.IsInRole("Teacher"))
                MonitoringStates[pcId] = false;

            return Ok(new
            {
                message = "Screen monitoring data removed.",
                pcId
            });
        }

        private async Task<bool> CanStudentAccessPcAsync(int pcId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Teacher"))
                return true;

            if (!User.IsInRole("Student"))
                return false;

            string? claimUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(claimUserId, out int authenticatedUserId))
                return false;

            return await _context.PCs
                .AsNoTracking()
                .AnyAsync(pc =>
                    pc.PCId == pcId &&
                    pc.CurrentUserId == authenticatedUserId &&
                    pc.IsEnabled);
        }
    }
}
