using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScreenMonitorController : ControllerBase
    {
        // ==========================================
        // LATEST SCREEN FRAME PER PC
        // ==========================================

        private static readonly ConcurrentDictionary<int, byte[]>
            LatestScreens = new();


        // ==========================================
        // MONITORING STATE PER PC
        // ==========================================

        private static readonly ConcurrentDictionary<int, bool>
            MonitoringStates = new();


        // ==========================================
        // GET MONITORING STATUS
        //
        // GET:
        // api/ScreenMonitor/{pcId}/status
        // ==========================================

        [HttpGet("{pcId}/status")]
        public IActionResult GetMonitoringStatus(
            int pcId)
        {
            bool enabled =
                MonitoringStates.TryGetValue(
                    pcId,
                    out bool state
                ) && state;


            return Ok(new
            {
                pcId = pcId,
                monitoring = enabled
            });
        }


        // ==========================================
        // START MONITORING
        //
        // POST:
        // api/ScreenMonitor/{pcId}/start
        // ==========================================

        [HttpPost("{pcId}/start")]
        public IActionResult StartMonitoring(
            int pcId)
        {
            MonitoringStates[pcId] = true;


            return Ok(new
            {
                message =
                    "Screen monitoring started.",

                pcId = pcId,

                monitoring = true
            });
        }


        // ==========================================
        // STOP MONITORING
        //
        // POST:
        // api/ScreenMonitor/{pcId}/stop
        // ==========================================

        [HttpPost("{pcId}/stop")]
        public IActionResult StopMonitoring(
            int pcId)
        {
            MonitoringStates[pcId] = false;


            // Remove latest screen frame.

            LatestScreens.TryRemove(
                pcId,
                out _
            );


            return Ok(new
            {
                message =
                    "Screen monitoring stopped.",

                pcId = pcId,

                monitoring = false
            });
        }


        // ==========================================
        // UPLOAD SCREEN
        //
        // POST:
        // api/ScreenMonitor/{pcId}
        //
        // Receives RAW JPEG bytes.
        // ==========================================

        [HttpPost("{pcId}")]
        public async Task<IActionResult> UploadScreen(
            int pcId)
        {
            // ==========================================
            // CHECK MONITORING
            // ==========================================

            bool enabled =
                MonitoringStates.TryGetValue(
                    pcId,
                    out bool state
                ) && state;


            if (!enabled)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "Screen monitoring is not enabled."
                    }
                );
            }


            // ==========================================
            // CHECK REQUEST BODY
            // ==========================================

            if (Request.ContentLength == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Screen image is empty."
                });
            }


            // ==========================================
            // READ RAW IMAGE
            // ==========================================

            using MemoryStream stream =
                new MemoryStream();


            await Request.Body.CopyToAsync(
                stream
            );


            byte[] image =
                stream.ToArray();


            // ==========================================
            // VALIDATE IMAGE
            // ==========================================

            if (image.Length == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Screen image is empty."
                });
            }


            // ==========================================
            // STORE LATEST SCREEN
            // ==========================================

            LatestScreens[pcId] =
                image;


            return Ok(new
            {
                message =
                    "Screen uploaded successfully.",

                pcId = pcId,

                size = image.Length
            });
        }


        // ==========================================
        // GET LATEST SCREEN
        //
        // GET:
        // api/ScreenMonitor/{pcId}
        // ==========================================

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
                "image/jpeg"
            );
        }


        // ==========================================
        // REMOVE SCREEN DATA
        //
        // DELETE:
        // api/ScreenMonitor/{pcId}
        // ==========================================

        [HttpDelete("{pcId}")]
        public IActionResult RemoveScreen(
            int pcId)
        {
            LatestScreens.TryRemove(
                pcId,
                out _
            );


            MonitoringStates[pcId] =
                false;


            return Ok(new
            {
                message =
                    "Screen monitoring data removed.",

                pcId = pcId
            });
        }
    }
}