using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class PCController : ControllerBase
{
    private readonly AppDbContext _context;

    public PCController(AppDbContext context) => _context = context;

    private async Task LogActivity(int? userId, int? pcId, string action, string details)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            PCId = pcId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAllPCs()
    {
        var pcs = await _context.PCs
            .Include(p => p.CurrentUser)
            .Include(p => p.Laboratory)
            .OrderBy(p => p.PCId)
            .Select(p => new
            {
                p.PCId,
                p.PCNumber,
                p.Status,
                p.CurrentUserId,
                Username = p.CurrentUser != null ? p.CurrentUser.Username : null,
                p.LaboratoryId,
                LaboratoryName = p.Laboratory != null ? p.Laboratory.LabName : null,
                p.MACAddress,
                p.IPAddress,
                p.LastSeen,
                p.IsEnabled,
                p.MaintenanceReason,
                p.MaintenanceStarted
            })
            .ToListAsync();

        return Ok(pcs);
    }

    [HttpGet("laboratory/{laboratoryId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetPCsByLaboratory(int laboratoryId)
    {
        bool laboratoryExists = await _context.Laboratories.AnyAsync(l => l.LaboratoryId == laboratoryId);
        if (!laboratoryExists)
            return NotFound(new { message = "Laboratory not found." });

        var pcs = await _context.PCs
            .Include(p => p.CurrentUser)
            .Include(p => p.Laboratory)
            .Where(p => p.LaboratoryId == laboratoryId)
            .OrderBy(p => p.PCNumber)
            .Select(p => new
            {
                p.PCId,
                p.PCNumber,
                p.Status,
                p.CurrentUserId,
                Username = p.CurrentUser != null ? p.CurrentUser.Username : null,
                p.LaboratoryId,
                LaboratoryName = p.Laboratory != null ? p.Laboratory.LabName : null,
                p.MACAddress,
                p.IPAddress,
                p.LastSeen,
                p.IsEnabled,
                p.MaintenanceReason,
                p.MaintenanceStarted
            })
            .ToListAsync();

        return Ok(pcs);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddPC([FromBody] PCCreateRequest request)
    {
        string pcNumber = request.PCNumber?.Trim() ?? string.Empty;
        string? macAddress = MachineIdentity.NormalizeMac(request.MACAddress);
        string? ipAddress = MachineIdentity.NormalizeIp(request.IPAddress);

        if (string.IsNullOrWhiteSpace(pcNumber))
            return BadRequest(new { message = "PC number is required." });
        if (macAddress == null)
            return BadRequest(new { message = "A valid MAC address is required for PC registration." });
        if (!string.IsNullOrWhiteSpace(request.IPAddress) && ipAddress == null)
            return BadRequest(new { message = "The supplied IP address is invalid." });

        Laboratory? laboratory = null;
        if (request.LaboratoryId.HasValue)
        {
            laboratory = await _context.Laboratories.FirstOrDefaultAsync(l => l.LaboratoryId == request.LaboratoryId.Value);
            if (laboratory == null)
                return BadRequest(new { message = "Selected laboratory does not exist." });
        }

        bool duplicateNumber = await _context.PCs.AnyAsync(p => p.PCNumber.ToLower() == pcNumber.ToLower());
        if (duplicateNumber)
            return Conflict(new { message = $"PC {pcNumber} is already registered." });

        bool duplicateMac = await _context.PCs.AnyAsync(p => p.MACAddress != null && p.MACAddress.ToLower() == macAddress.ToLower());
        if (duplicateMac)
            return Conflict(new { message = $"MAC address {macAddress} is already registered." });

        var pc = new PC
        {
            PCNumber = pcNumber,
            Status = "Available",
            LaboratoryId = request.LaboratoryId,
            Laboratory = laboratory,
            MACAddress = macAddress,
            IPAddress = ipAddress,
            IsEnabled = true
        };

        _context.PCs.Add(pc);
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "PC Added", $"PC {pc.PCNumber} was added to SmartLab.");

        return Ok(new
        {
            message = "PC added successfully.",
            pcId = pc.PCId,
            pcNumber = pc.PCNumber,
            status = pc.Status,
            laboratoryId = pc.LaboratoryId,
            laboratoryName = laboratory?.LabName,
            macAddress = pc.MACAddress,
            ipAddress = pc.IPAddress,
            isEnabled = pc.IsEnabled
        });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetPC(int id)
    {
        var pc = await _context.PCs
            .Include(p => p.CurrentUser)
            .Include(p => p.Laboratory)
            .Where(p => p.PCId == id)
            .Select(p => new
            {
                p.PCId,
                p.PCNumber,
                p.Status,
                p.CurrentUserId,
                Username = p.CurrentUser != null ? p.CurrentUser.Username : null,
                p.LaboratoryId,
                LaboratoryName = p.Laboratory != null ? p.Laboratory.LabName : null,
                p.MACAddress,
                p.IPAddress,
                p.LastSeen,
                p.IsEnabled,
                p.MaintenanceReason,
                p.MaintenanceStarted
            })
            .FirstOrDefaultAsync();

        return pc == null ? NotFound(new { message = "PC not found." }) : Ok(pc);
    }

    [HttpPut("{id}/number")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePCNumber(int id, [FromBody] PCNumberUpdateRequest request)
    {
        string newPcNumber = request.PCNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newPcNumber))
            return BadRequest(new { message = "PC number is required." });

        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });
        if (pc.CurrentUserId.HasValue)
            return Conflict(new { message = "Cannot edit a PC number while the PC is occupied." });

        bool duplicate = await _context.PCs.AnyAsync(p => p.PCId != id && p.PCNumber.ToLower() == newPcNumber.ToLower());
        if (duplicate)
            return Conflict(new { message = $"PC {newPcNumber} is already registered." });

        string oldPcNumber = pc.PCNumber;
        pc.PCNumber = newPcNumber;
        await _context.SaveChangesAsync();
        await LogActivity(null, id, "PC Number Updated", $"PC number changed from {oldPcNumber} to {newPcNumber}.");

        return Ok(new { message = "PC number updated successfully.", pcId = pc.PCId, pcNumber = pc.PCNumber, status = pc.Status, isEnabled = pc.IsEnabled });
    }

    [HttpPut("{id}/laboratory")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignLaboratory(int id, [FromBody] PCLaboratoryRequest request)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });
        if (pc.CurrentUserId.HasValue)
            return Conflict(new { message = "Cannot change laboratory while the PC is occupied." });

        Laboratory? laboratory = null;
        if (request.LaboratoryId.HasValue)
        {
            laboratory = await _context.Laboratories.FirstOrDefaultAsync(l => l.LaboratoryId == request.LaboratoryId.Value);
            if (laboratory == null)
                return BadRequest(new { message = "Laboratory not found." });
        }

        int? oldLaboratoryId = pc.LaboratoryId;
        pc.LaboratoryId = request.LaboratoryId;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "PC Laboratory Updated", $"PC {pc.PCNumber} laboratory changed from {oldLaboratoryId?.ToString() ?? "None"} to {request.LaboratoryId?.ToString() ?? "None"}.");

        return Ok(new { message = "PC laboratory updated successfully.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, laboratoryName = laboratory?.LabName });
    }

    [HttpPut("{id}/network")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateNetworkIdentity(int id, [FromBody] PCNetworkIdentityRequest request)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });

        string? mac = MachineIdentity.NormalizeMac(request.MACAddress);
        if (mac == null)
            return BadRequest(new { message = "A valid MAC address is required." });

        string? ip = MachineIdentity.NormalizeIp(request.IPAddress);
        if (!string.IsNullOrWhiteSpace(request.IPAddress) && ip == null)
            return BadRequest(new { message = "The supplied IP address is invalid." });

        bool duplicateMac = await _context.PCs.AnyAsync(p => p.PCId != id && p.MACAddress != null && p.MACAddress.ToLower() == mac.ToLower());
        if (duplicateMac)
            return Conflict(new { message = $"MAC address {mac} is already registered to another PC." });

        pc.MACAddress = mac;
        pc.IPAddress = ip;
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "PC Network Identity Updated", $"PC {pc.PCNumber} network identity was updated.");

        return Ok(new { message = "PC network identity updated successfully.", pcId = pc.PCId, pcNumber = pc.PCNumber, macAddress = pc.MACAddress, ipAddress = pc.IPAddress, lastSeen = pc.LastSeen });
    }

    [HttpPost("login/{pcNumber}/{userId}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> LoginToPC(string pcNumber, int userId, [FromBody] PCLoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.Role == "Student");
        if (user == null)
            return NotFound(new { message = "Student account not found." });

        string? macAddress = MachineIdentity.NormalizeMac(request.MACAddress);
        if (macAddress == null)
            return BadRequest(new { message = "A valid machine MAC address is required." });

        string normalizedPcNumber = pcNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPcNumber))
            return BadRequest(new { message = "PC number is required." });

        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCNumber.ToLower() == normalizedPcNumber.ToLower());
        if (pc == null)
            return NotFound(new { message = $"PC {normalizedPcNumber} is not registered in SmartLab. An Admin/MIS must register and provision this workstation first." });

        string? registeredMac = MachineIdentity.NormalizeMac(pc.MACAddress);
        if (registeredMac == null)
            return Conflict(new { message = $"PC {pc.PCNumber} has no valid registered MAC address. An Admin/MIS must provision its machine identity." });
        if (!string.Equals(registeredMac, macAddress, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This workstation does not match the registered PC identity." });

        if (!pc.IsEnabled)
            return Conflict(new { message = $"PC {pc.PCNumber} is currently disabled." });
        if (pc.Status == "Maintenance")
            return Conflict(new { message = $"PC {pc.PCNumber} is currently under maintenance." });
        if (pc.Status != "Available" || pc.CurrentUserId.HasValue)
            return Conflict(new { message = $"PC {pc.PCNumber} is not available for a new student session." });

        pc.Status = "Occupied";
        pc.CurrentUserId = userId;
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(userId, pc.PCId, "PC Login", $"User {user.Username} logged in to PC {pc.PCNumber}.");

        return Ok(new
        {
            message = "PC login successful.",
            pcId = pc.PCId,
            pcNumber = pc.PCNumber,
            status = pc.Status,
            userId = user.UserId,
            username = user.Username,
            laboratoryId = pc.LaboratoryId,
            lastSeen = pc.LastSeen,
            isEnabled = pc.IsEnabled
        });
    }

    [HttpPost("release/{userId}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> ReleasePC(int userId)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.CurrentUserId == userId);
        if (pc == null)
            return NotFound(new { message = "No PC is currently assigned to this user." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        string username = user?.Username ?? $"User {userId}";
        string pcNumber = pc.PCNumber;

        pc.Status = "Available";
        pc.CurrentUserId = null;
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(userId, pc.PCId, "PC Released", $"PC {pcNumber} released by user {username}.");

        return Ok(new { message = "PC released successfully.", pcId = pc.PCId, pcNumber, status = pc.Status, currentUserId = (int?)null, laboratoryId = pc.LaboratoryId, lastSeen = pc.LastSeen, isEnabled = pc.IsEnabled });
    }

    [HttpPost("{id}/heartbeat")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Heartbeat(int id)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });

        pc.LastSeen = DateTime.Now;
        if (pc.Status == "Offline" && pc.CurrentUserId.HasValue && pc.IsEnabled)
            pc.Status = "Occupied";

        await _context.SaveChangesAsync();
        return Ok(new { message = "Heartbeat received.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, status = pc.Status, lastSeen = pc.LastSeen, isEnabled = pc.IsEnabled });
    }

    [HttpPut("{id}/enabled")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEnabled(int id, [FromBody] PCEnabledRequest request)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });

        if (!request.IsEnabled && pc.CurrentUserId.HasValue)
            return Conflict(new { message = "Cannot disable a PC while it is assigned to a student." });

        pc.IsEnabled = request.IsEnabled;
        if (!pc.IsEnabled && pc.Status == "Available")
            pc.Status = "Maintenance";
        else if (pc.IsEnabled && pc.Status == "Maintenance" && pc.CurrentUserId == null && string.IsNullOrWhiteSpace(pc.MaintenanceReason))
            pc.Status = "Available";

        if (pc.IsEnabled)
        {
            pc.MaintenanceReason = null;
            pc.MaintenanceStarted = null;
        }

        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, pc.IsEnabled ? "PC Enabled" : "PC Disabled", pc.IsEnabled ? $"PC {pc.PCNumber} was enabled." : $"PC {pc.PCNumber} was disabled.");

        return Ok(new { message = pc.IsEnabled ? "PC enabled successfully." : "PC disabled successfully.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, status = pc.Status, isEnabled = pc.IsEnabled, maintenanceReason = pc.MaintenanceReason, maintenanceStarted = pc.MaintenanceStarted, lastSeen = pc.LastSeen });
    }

    [HttpPut("{id}/maintenance")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetMaintenance(int id, [FromBody] PCMaintenanceRequest request)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });

        if (pc.CurrentUserId.HasValue)
            return Conflict(new { message = "Cannot put a PC into maintenance while it is assigned to a student." });
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Maintenance reason is required." });

        pc.Status = "Maintenance";
        pc.IsEnabled = false;
        pc.CurrentUserId = null;
        pc.MaintenanceReason = request.Reason.Trim();
        pc.MaintenanceStarted = DateTime.Now;
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "Maintenance Started", $"PC {pc.PCNumber} placed under maintenance. Reason: {pc.MaintenanceReason}");

        return Ok(new { message = "PC placed under maintenance.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, status = pc.Status, isEnabled = pc.IsEnabled, maintenanceReason = pc.MaintenanceReason, maintenanceStarted = pc.MaintenanceStarted, lastSeen = pc.LastSeen });
    }

    [HttpPut("{id}/maintenance/clear")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClearMaintenance(int id)
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });

        pc.Status = "Available";
        pc.IsEnabled = true;
        pc.CurrentUserId = null;
        pc.MaintenanceReason = null;
        pc.MaintenanceStarted = null;
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "Maintenance Cleared", $"Maintenance cleared for PC {pc.PCNumber}. PC is now available.");

        return Ok(new { message = "PC maintenance cleared.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, status = pc.Status, isEnabled = pc.IsEnabled, maintenanceReason = pc.MaintenanceReason, maintenanceStarted = pc.MaintenanceStarted, lastSeen = pc.LastSeen });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] PCStatusRequest request)
    {
        string status = request.Status?.Trim() ?? string.Empty;
        string[] validStates = { "Available", "Occupied", "Offline", "Maintenance" };
        if (!validStates.Contains(status, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "PC status must be Available, Occupied, Offline, or Maintenance." });
        if (status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Use the maintenance endpoint to place a PC into maintenance." });

        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == id);
        if (pc == null)
            return NotFound(new { message = "PC not found." });
        if (status.Equals("Available", StringComparison.OrdinalIgnoreCase) && pc.CurrentUserId.HasValue)
            return Conflict(new { message = "An occupied PC cannot be manually marked Available. Release the student session first." });
        if (status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) && (pc.CurrentUserId == null || !pc.IsEnabled))
            return Conflict(new { message = "A PC can only be marked Occupied when it is enabled and has a current student owner." });
        if (pc.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) && !pc.IsEnabled)
            return Conflict(new { message = "Clear maintenance before manually changing this PC status." });

        pc.Status = validStates.First(v => v.Equals(status, StringComparison.OrdinalIgnoreCase));
        pc.LastSeen = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogActivity(null, pc.PCId, "PC Status Updated", $"PC {pc.PCNumber} status changed to {pc.Status}.");

        return Ok(new { message = "PC status updated successfully.", pcId = pc.PCId, pcNumber = pc.PCNumber, laboratoryId = pc.LaboratoryId, status = pc.Status, lastSeen = pc.LastSeen, isEnabled = pc.IsEnabled });
    }
}

public sealed class PCCreateRequest
{
    public string PCNumber { get; set; } = string.Empty;
    public int? LaboratoryId { get; set; }
    public string? MACAddress { get; set; }
    public string? IPAddress { get; set; }
}

public sealed class PCNumberUpdateRequest
{
    public string PCNumber { get; set; } = string.Empty;
}

public sealed class PCLaboratoryRequest
{
    public int? LaboratoryId { get; set; }
}

public sealed class PCNetworkIdentityRequest
{
    public string? MACAddress { get; set; }
    public string? IPAddress { get; set; }
}

public sealed class PCLoginRequest
{
    public string MACAddress { get; set; } = string.Empty;
}

public sealed class PCStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public sealed class PCEnabledRequest
{
    public bool IsEnabled { get; set; }
}

public sealed class PCMaintenanceRequest
{
    public string Reason { get; set; } = string.Empty;
}
