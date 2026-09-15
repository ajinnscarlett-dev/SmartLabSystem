using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace SmartLab.Server.Controllers
{
    [Route("api/PC/presence")]
    [ApiController]
    [AllowAnonymous]
    public class PCPresenceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PCPresenceController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Announce(
            [FromBody] MachinePresenceRequest request)
        {
            string pcNumber = request.PCNumber?.Trim() ?? string.Empty;
            string macAddress = request.MACAddress?.Trim() ?? string.Empty;
            string? ipAddress = string.IsNullOrWhiteSpace(request.IPAddress)
                ? null
                : request.IPAddress.Trim();

            if (string.IsNullOrWhiteSpace(pcNumber) ||
                string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(new { message = "PC number and MAC address are required." });
            }

            if (!IsValidMac(macAddress))
            {
                return BadRequest(new { message = "Invalid MAC address." });
            }

            if (ipAddress != null && !IPAddress.TryParse(ipAddress, out _))
            {
                return BadRequest(new { message = "Invalid IP address." });
            }

            var pc = await _context.PCs
                .FirstOrDefaultAsync(p => p.PCNumber == pcNumber);

            if (pc == null)
            {
                return NotFound(new { message = "This workstation is not registered." });
            }

            string normalizedIncomingMac = NormalizeMac(macAddress);
            string? normalizedStoredMac = string.IsNullOrWhiteSpace(pc.MACAddress)
                ? null
                : NormalizeMac(pc.MACAddress);

            if (normalizedStoredMac == null)
            {
                // Do not let anonymous clients self-register a permanent identity.
                // Initial MAC binding must be performed by an authorized admin/registration workflow.
                return Conflict(new { message = "This workstation has no registered MAC address." });
            }

            if (!string.Equals(normalizedStoredMac, normalizedIncomingMac, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = "Machine identity does not match the registered workstation." });
            }

            bool macBelongsElsewhere = await _context.PCs.AnyAsync(p =>
                p.PCId != pc.PCId &&
                p.MACAddress != null &&
                p.MACAddress.ToLower() == normalizedIncomingMac.ToLower());

            if (macBelongsElsewhere)
            {
                return Conflict(new { message = "This MAC address is already registered to another workstation." });
            }

            pc.IPAddress = ipAddress;
            pc.LastSeen = DateTime.Now;

            if (!string.Equals(pc.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                pc.Status = pc.CurrentUserId.HasValue ? "Occupied" : "Available";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                laboratoryId = pc.LaboratoryId,
                status = pc.Status,
                ipAddress = pc.IPAddress,
                lastSeen = pc.LastSeen
            });
        }

        private static string NormalizeMac(string value)
        {
            return value
                .Trim()
                .Replace(":", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .ToUpperInvariant();
        }

        private static bool IsValidMac(string value)
        {
            string normalized = NormalizeMac(value);
            return normalized.Length == 12 &&
                   normalized.All(c =>
                       (c >= '0' && c <= '9') ||
                       (c >= 'A' && c <= 'F'));
        }
    }

    public class MachinePresenceRequest
    {
        public string PCNumber { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
    }
}
