using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers;

[Route("api/PC")]
[ApiController]
public sealed class MachinePresenceController : ControllerBase
{
    private readonly AppDbContext _context;

    public MachinePresenceController(AppDbContext context) => _context = context;

    [AllowAnonymous]
    [HttpPost("presence")]
    public async Task<IActionResult> AnnouncePresence(
        [FromBody] MachinePresenceRequest request,
        CancellationToken cancellationToken)
    {
        string pcNumber = request.PCNumber?.Trim() ?? string.Empty;
        string? macAddress = MachineIdentity.NormalizeMac(request.MACAddress);

        if (string.IsNullOrWhiteSpace(pcNumber) || macAddress == null)
            return BadRequest(new { message = "PC number and a valid MAC address are required." });

        var pc = await _context.PCs
            .FirstOrDefaultAsync(p => p.PCNumber == pcNumber, cancellationToken);

        if (pc == null)
            return NotFound(new { message = "This PC is not registered in SmartLab." });

        string? registeredMac = MachineIdentity.NormalizeMac(pc.MACAddress);
        if (registeredMac == null)
            return Conflict(new { message = "This PC has no registered MAC address. An Admin/MIS must provision its machine identity first." });

        if (!string.Equals(registeredMac, macAddress, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Machine identity does not match the registered PC." });

        string? ipAddress = MachineIdentity.NormalizeIp(request.IPAddress);
        if (!string.IsNullOrWhiteSpace(request.IPAddress) && ipAddress == null)
            return BadRequest(new { message = "The supplied IP address is invalid." });

        ipAddress ??= MachineIdentity.NormalizeIp(HttpContext.Connection.RemoteIpAddress?.ToString());

        pc.IPAddress = ipAddress;
        pc.LastSeen = DateTime.Now;

        // Maintenance and disabled workstations must never be made Available/Occupied by anonymous presence.
        if (pc.Status != "Maintenance" && pc.IsEnabled)
            pc.Status = pc.CurrentUserId.HasValue ? "Occupied" : "Available";

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
}

public sealed class MachinePresenceRequest
{
    public string PCNumber { get; set; } = string.Empty;
    public string MACAddress { get; set; } = string.Empty;
    public string? IPAddress { get; set; }
}
