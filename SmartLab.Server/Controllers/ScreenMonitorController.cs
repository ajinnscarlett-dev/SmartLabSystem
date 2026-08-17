using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScreenMonitorController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static readonly ConcurrentDictionary<int, byte[]>
            LatestScreens = new();

        private static readonly ConcurrentDictionary<int, bool>
            MonitoringStates = new();

        public ScreenMonitorController(
            AppDbContext context)
        {
            _context = context;
        }

        // ==========================================================
        // GET MONITORING STATUS
        // ==========================================================
        //
        // Admin / Teacher can query any PC.
        // Student can query only the PC currently assigned
        // to that authenticated student.
        //
        // The Student client needs this endpoint to know when
        // it should start uploading screenshots.
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpGet("{pcId}/status")]
        public async Task<IActionResult> GetMonitoringStatus(
            int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
            {
                return Forbid();
            }

            bool enabled =
                MonitoringStates.TryGetValue(
                    pcId,
                    out bool state) && state;

            return Ok(new
            {
                pcId,
                monitoring = enabled
            });
        }

        // ==========================================================
        // START MONITORING
        // ==========================================================
        //
        // Only Admin / Teacher can start monitoring.
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/start")]
        public IActionResult StartMonitoring(
            int pcId)
        {
            MonitoringStates[pcId] = true;

            return Ok(new
            {
                message =
                    "Screen monitoring started.",

                pcId,
                monitoring = true
            });
        }

        // ==========================================================
        // STOP MONITORING
        // ==========================================================
        //
        // Only Admin / Teacher can stop monitoring.
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/stop")]
        public IActionResult StopMonitoring(
            int pcId)
        {
            MonitoringStates[pcId] = false;

            LatestScreens.TryRemove(
                pcId,
                out _);

            return Ok(new
            {
                message =
                    "Screen monitoring stopped.",

                pcId,
                monitoring = false
            });
        }

        // ==========================================================
        // UPLOAD SCREEN
        // ==========================================================
        //
        // Student clients upload only their own assigned PC.
        // Admin / Teacher are NOT allowed to upload here.
        // ==========================================================

        [Authorize(Roles = "Student")]
        [HttpPost("{pcId}")]
        public async Task<IActionResult> UploadScreen(
            int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
            {
                return Forbid();
            }

            bool enabled =
                MonitoringStates.TryGetValue(
                    pcId,
                    out bool state) && state;

            if (!enabled)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "Screen monitoring is not enabled."
                    });
            }

            if (Request.ContentLength == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Screen image is empty."
                });
            }

            using MemoryStream stream =
                new MemoryStream();

            await Request.Body.CopyToAsync(
                stream);

            byte[] image =
                stream.ToArray();

            if (image.Length == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Screen image is empty."
                });
            }

            LatestScreens[pcId] =
                image;

            return Ok(new
            {
                message =
                    "Screen uploaded successfully.",

                pcId,
                size = image.Length
            });
        }

        // ==========================================================
        // GET LATEST SCREEN
        // ==========================================================
        //
        // Admin / Teacher only.
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{pcId}")]
        public IActionResult GetScreen(
            int pcId)
        {
            if (!LatestScreens.TryGetValue(
                pcId,
                out byte[]? image))
            {
                return NotFound(new
                {
                    message =
                        "No screen image available for this PC."
                });
            }

            return File(
                image,
                "image/jpeg");
        }

        // ==========================================================
        // REMOVE SCREEN DATA
        // ==========================================================
        //
        // Admin / Teacher can remove any PC's screen.
        // Student can remove only their own PC's screen.
        //
        // This keeps the existing Student logout cleanup working.
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpDelete("{pcId}")]
        public async Task<IActionResult> RemoveScreen(
            int pcId)
        {
            if (!await CanStudentAccessPcAsync(pcId))
            {
                return Forbid();
            }

            LatestScreens.TryRemove(
                pcId,
                out _);

            // A Student clearing their own screen data should
            // not control whether Admin monitoring is enabled.
            //
            // Admin / Teacher stop-monitoring endpoint is the
            // authoritative switch.
            if (User.IsInRole("Admin") ||
                User.IsInRole("Teacher"))
            {
                MonitoringStates[pcId] = false;
            }

            return Ok(new
            {
                message =
                    "Screen monitoring data removed.",

                pcId
            });
        }

        // ==========================================================
        // STUDENT ACCESS CHECK
        // ==========================================================
        //
        // Admin / Teacher:
        //     allowed for every PC.
        //
        // Student:
        //     allowed only when the PC's CurrentUserId is the
        //     authenticated student's UserId.
        // ==========================================================

        private async Task<bool> CanStudentAccessPcAsync(
            int pcId)
        {
            if (User.IsInRole("Admin") ||
                User.IsInRole("Teacher"))
            {
                return true;
            }

            if (!User.IsInRole("Student"))
            {
                return false;
            }

            string? claimUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            if (!int.TryParse(
                claimUserId,
                out int authenticatedUserId))
            {
                return false;
            }

            return await _context.PCs
                .AsNoTracking()
                .AnyAsync(
                    pc =>
                        pc.PCId == pcId &&
                        pc.CurrentUserId ==
                            authenticatedUserId &&
                        pc.IsEnabled);
        }
    }
}
