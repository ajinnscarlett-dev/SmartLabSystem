using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace SmartLab.Server.Controllers
{
    [Route("api/PC")]
    [ApiController]
    public sealed class MachinePresenceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MachinePresenceController(AppDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpPost("presence")]
        public async Task<IActionResult> AnnouncePresence(
            [FromBody] MachinePresenceRequest request,
            CancellationToken cancellationToken)
        {
            string pcNumber = request.PCNumber?.Trim() ?? string.Empty;
            string macAddress = NormalizeMac(request.MACAddress);

            if (string.IsNullOrWhiteSpace(pcNumber) || macAddress == null)
            {
                return BadRequest(new
                {
                    message = "PC number and a valid MAC address are required."
                });
            }

            var pc = await _context.PCs
                .FirstOrDefaultAsync(
                    p => p.PCNumber == pcNumber,
                    cancellationToken);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "This PC is not registered in SmartLab."
                });
            }

            // The physical MAC address is the trust anchor for anonymous
            // machine presence. A client cannot choose another PC number.
            string? registeredMac = NormalizeMac(pc.MACAddress);

            if (registeredMac == null)
            {
                return Conflict(new
                {
                    message = "This PC has no registered MAC address. An Admin/MIS must provision its machine identity first."
                });
            }

            if (!string.Equals(
                    registeredMac,
                    macAddress,
                    StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Machine identity does not match the registered PC."
                });
            }

            string? ipAddress = NormalizeIp(request.IPAddress);

            if (ipAddress == null)
            {
                ipAddress = NormalizeIp(
                    HttpContext.Connection.RemoteIpAddress?.ToString());
            }

            pc.IPAddress = ipAddress;
            pc.LastSeen = DateTime.Now;

            // Maintenance is an explicit administrative state and always wins.
            // Otherwise derive the visible state from whether a student owns it.
            if (pc.Status != "Maintenance")
            {
                pc.Status = pc.CurrentUserId.HasValue
                    ? "Occupied"
                    : "Available";
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "Machine presence received.",
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                laboratoryId = pc.LaboratoryId,
                status = pc.Status,
                ipAddress = pc.IPAddress,
                lastSeen = pc.LastSeen,
                isEnabled = pc.IsEnabled
            });
        }

        private static string? NormalizeMac(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = Regex.Replace(
                value.Trim(),
                "[:-]",
                string.Empty);

            if (!Regex.IsMatch(normalized, "^[0-9A-Fa-f]{12}$"))
            {
                return null;
            }

            return normalized.ToUpperInvariant();
        }

        private static string? NormalizeIp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!IPAddress.TryParse(value.Trim(), out IPAddress? address))
            {
                return null;
            }

            return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? address.MapToIPv4().ToString()
                : address.ToString();
        }
    }

    public sealed class MachinePresenceRequest
    {
        public string PCNumber { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
    }
}
