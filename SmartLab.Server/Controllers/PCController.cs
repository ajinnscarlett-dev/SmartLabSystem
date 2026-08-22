using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PCController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PCController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ACTIVITY LOG HELPER
        // ==========================================

        private async Task LogActivity(
            int? userId,
            int? pcId,
            string action,
            string details)
        {
            var log = new ActivityLog
            {
                UserId = userId,
                PCId = pcId,
                Action = action,
                Details = details,
                CreatedAt = DateTime.Now
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        // ==========================================
        // GET ALL PCs
        // ==========================================

        [HttpGet]
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

                    Username = p.CurrentUser != null
                        ? p.CurrentUser.Username
                        : null,

                    p.LaboratoryId,

                    LaboratoryName = p.Laboratory != null
                        ? p.Laboratory.LabName
                        : null,

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

        // ==========================================
        // GET PCs BY LABORATORY
        // ==========================================

        [HttpGet("laboratory/{laboratoryId}")]
        public async Task<IActionResult> GetPCsByLaboratory(
            int laboratoryId)
        {
            var laboratory = await _context.Laboratories
                .FirstOrDefaultAsync(l =>
                    l.LaboratoryId == laboratoryId);

            if (laboratory == null)
            {
                return NotFound(new
                {
                    message = "Laboratory not found."
                });
            }

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

                    Username = p.CurrentUser != null
                        ? p.CurrentUser.Username
                        : null,

                    p.LaboratoryId,

                    LaboratoryName = p.Laboratory != null
                        ? p.Laboratory.LabName
                        : null,

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

        // ==========================================
        // ADD NEW PC
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> AddPC(
            [FromBody] PCCreateRequest request)
        {
            string pcNumber =
                request.PCNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pcNumber))
            {
                return BadRequest(new
                {
                    message = "PC number is required."
                });
            }

            // ==========================================
            // VALIDATE LABORATORY IF PROVIDED
            // ==========================================

            Laboratory? laboratory = null;

            if (request.LaboratoryId.HasValue)
            {
                laboratory = await _context.Laboratories
                    .FirstOrDefaultAsync(l =>
                        l.LaboratoryId ==
                        request.LaboratoryId.Value);

                if (laboratory == null)
                {
                    return BadRequest(new
                    {
                        message = "Selected laboratory does not exist."
                    });
                }
            }

            // ==========================================
            // CHECK DUPLICATE PC NUMBER
            // ==========================================

            var existingPC =
                await _context.PCs
                    .FirstOrDefaultAsync(p =>
                        p.PCNumber.ToLower() ==
                        pcNumber.ToLower());

            if (existingPC != null)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pcNumber} is already registered."
                });
            }

            // ==========================================
            // CHECK DUPLICATE MAC
            // ==========================================

            if (!string.IsNullOrWhiteSpace(request.MACAddress))
            {
                string mac =
                    request.MACAddress.Trim();

                var existingMac =
                    await _context.PCs
                        .FirstOrDefaultAsync(p =>
                            p.MACAddress != null &&
                            p.MACAddress.ToLower() ==
                            mac.ToLower());

                if (existingMac != null)
                {
                    return Conflict(new
                    {
                        message =
                            $"MAC address {mac} is already registered."
                    });
                }
            }

            // ==========================================
            // CREATE PC
            // ==========================================

            var pc = new PC
            {
                PCNumber = pcNumber,
                Status = "Available",
                CurrentUserId = null,
                LaboratoryId = request.LaboratoryId,
                Laboratory = laboratory,
                MACAddress =
                    string.IsNullOrWhiteSpace(request.MACAddress)
                        ? null
                        : request.MACAddress.Trim(),
                IPAddress =
                    string.IsNullOrWhiteSpace(request.IPAddress)
                        ? null
                        : request.IPAddress.Trim(),
                LastSeen = null,
                IsEnabled = true,
                MaintenanceReason = null,
                MaintenanceStarted = null
            };

            _context.PCs.Add(pc);

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "PC Added",
                $"PC {pc.PCNumber} was added to SmartLab."
            );

            return Ok(new
            {
                message = "PC added successfully.",
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                status = pc.Status,
                laboratoryId = pc.LaboratoryId,
                laboratoryName =
                    laboratory?.LabName,
                macAddress = pc.MACAddress,
                ipAddress = pc.IPAddress,
                isEnabled = pc.IsEnabled
            });
        }

        // ==========================================
        // GET ONE PC
        // ==========================================

        [HttpGet("{id}")]
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

                    Username = p.CurrentUser != null
                        ? p.CurrentUser.Username
                        : null,

                    p.LaboratoryId,

                    LaboratoryName = p.Laboratory != null
                        ? p.Laboratory.LabName
                        : null,

                    p.MACAddress,
                    p.IPAddress,
                    p.LastSeen,
                    p.IsEnabled,
                    p.MaintenanceReason,
                    p.MaintenanceStarted
                })
                .FirstOrDefaultAsync();

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            return Ok(pc);
        }

        // ==========================================
        // EDIT PC NUMBER
        // ==========================================

        [HttpPut("{id}/number")]
        public async Task<IActionResult> UpdatePCNumber(
            int id,
            [FromBody] PCNumberUpdateRequest request)
        {
            string newPcNumber =
                request.PCNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newPcNumber))
            {
                return BadRequest(new
                {
                    message = "PC number is required."
                });
            }

            var pc = await _context.PCs
                .FirstOrDefaultAsync(p => p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            if (pc.Status == "Occupied" ||
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        "Cannot edit a PC number while the PC is occupied."
                });
            }

            var duplicatePC =
                await _context.PCs
                    .FirstOrDefaultAsync(p =>
                        p.PCId != id &&
                        p.PCNumber.ToLower() ==
                        newPcNumber.ToLower());

            if (duplicatePC != null)
            {
                return Conflict(new
                {
                    message =
                        $"PC {newPcNumber} is already registered."
                });
            }

            string oldPcNumber = pc.PCNumber;

            pc.PCNumber = newPcNumber;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "PC Number Updated",
                $"PC number changed from {oldPcNumber} to {pc.PCNumber}."
            );

            return Ok(new
            {
                message = "PC number updated successfully.",
                pcId = pc.PCId,
                oldPcNumber = oldPcNumber,
                pcNumber = pc.PCNumber,
                status = pc.Status,
                isEnabled = pc.IsEnabled
            });
        }

        // ==========================================
        // ASSIGN PC TO LABORATORY
        // ==========================================

        [HttpPut("{id}/laboratory")]
        public async Task<IActionResult> AssignLaboratory(
            int id,
            [FromBody] PCLaboratoryRequest request)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            Laboratory? laboratory = null;

            if (request.LaboratoryId.HasValue)
            {
                laboratory = await _context.Laboratories
                    .FirstOrDefaultAsync(l =>
                        l.LaboratoryId ==
                        request.LaboratoryId.Value);

                if (laboratory == null)
                {
                    return BadRequest(new
                    {
                        message = "Laboratory not found."
                    });
                }
            }

            if (pc.Status == "Occupied" ||
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        "Cannot change laboratory while the PC is occupied."
                });
            }

            int? oldLaboratoryId =
                pc.LaboratoryId;

            pc.LaboratoryId =
                request.LaboratoryId;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "PC Laboratory Updated",
                $"PC {pc.PCNumber} laboratory changed from " +
                $"{oldLaboratoryId?.ToString() ?? "None"} to " +
                $"{request.LaboratoryId?.ToString() ?? "None"}."
            );

            return Ok(new
            {
                message =
                    "PC laboratory updated successfully.",
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                laboratoryId = pc.LaboratoryId,
                laboratoryName =
                    laboratory?.LabName
            });
        }

        // ==========================================
        // UPDATE PC NETWORK IDENTITY
        // ==========================================

        [HttpPut("{id}/network")]
        public async Task<IActionResult> UpdateNetworkIdentity(
            int id,
            [FromBody] PCNetworkIdentityRequest request)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            string? mac =
                string.IsNullOrWhiteSpace(request.MACAddress)
                    ? null
                    : request.MACAddress.Trim();

            string? ip =
                string.IsNullOrWhiteSpace(request.IPAddress)
                    ? null
                    : request.IPAddress.Trim();

            if (mac != null)
            {
                var duplicateMac =
                    await _context.PCs
                        .FirstOrDefaultAsync(p =>
                            p.PCId != id &&
                            p.MACAddress != null &&
                            p.MACAddress.ToLower() ==
                            mac.ToLower());

                if (duplicateMac != null)
                {
                    return Conflict(new
                    {
                        message =
                            $"MAC address {mac} is already registered to another PC."
                    });
                }
            }

            pc.MACAddress = mac;
            pc.IPAddress = ip;
            pc.LastSeen = DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "PC Network Identity Updated",
                $"PC {pc.PCNumber} network identity was updated."
            );

            return Ok(new
            {
                message =
                    "PC network identity updated successfully.",
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                macAddress = pc.MACAddress,
                ipAddress = pc.IPAddress,
                lastSeen = pc.LastSeen
            });
        }

        // ==========================================
        // LOGIN TO THIS PC
        // ==========================================

        [HttpPost("login/{pcNumber}/{userId}")]
        public async Task<IActionResult> LoginToPC(
            string pcNumber,
            int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCNumber.ToLower() == pcNumber.ToLower());

            // ==========================================
            // AUTO REGISTER THIS COMPUTER
            // ==========================================

            if (pc == null)
            {
                pc = new PC
                {
                    PCNumber = pcNumber.Trim(),
                    Status = "Available",
                    CurrentUserId = null,
                    LastSeen = DateTime.Now,
                    IsEnabled = true,
                    MaintenanceReason = null,
                    MaintenanceStarted = null
                };

                _context.PCs.Add(pc);

                await _context.SaveChangesAsync();

                await LogActivity(
                    userId,
                    pc.PCId,
                    "PC Auto Registered",
                    $"PC {pc.PCNumber} was automatically registered by SmartLab."
                );
            }

            if (!pc.IsEnabled)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is currently disabled."
                });
            }

            if (pc.Status == "Maintenance")
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is currently under maintenance."
                });
            }

            if (pc.Status == "Occupied" ||
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is already occupied."
                });
            }

            pc.Status = "Occupied";
            pc.CurrentUserId = userId;
            pc.LastSeen = DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                userId,
                pc.PCId,
                "PC Login",
                $"User {user.Username} logged in to PC {pc.PCNumber}."
            );

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

        // ==========================================
        // OLD AUTOMATIC ASSIGN PC
        // ==========================================
        //
        // KEPT FOR NOW
        // We will remove this later after the
        // Student Client has been converted to
        // the new "THIS PC" system.
        //

        [HttpPost("assign/{userId}")]
        public async Task<IActionResult> AssignPC(
            int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            var existingPC = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.CurrentUserId == userId);

            if (existingPC != null)
            {
                return Ok(new
                {
                    message =
                        "Student already has a PC.",
                    pcId =
                        existingPC.PCId,
                    pcNumber =
                        existingPC.PCNumber,
                    status =
                        existingPC.Status,
                    userId =
                        user.UserId,
                    username =
                        user.Username,
                    lastSeen =
                        existingPC.LastSeen,
                    isEnabled =
                        existingPC.IsEnabled,
                    maintenanceReason =
                        existingPC.MaintenanceReason,
                    laboratoryId =
                        existingPC.LaboratoryId
                });
            }

            var pc = await _context.PCs
                .OrderBy(p => p.PCId)
                .FirstOrDefaultAsync(p =>
                    p.Status == "Available" &&
                    p.CurrentUserId == null &&
                    p.IsEnabled == true);

            if (pc == null)
            {
                return Conflict(new
                {
                    message =
                        "No available PC."
                });
            }

            pc.Status =
                "Occupied";

            pc.CurrentUserId =
                userId;

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                userId,
                pc.PCId,
                "PC Assigned",
                $"PC {pc.PCNumber} assigned to user {user.Username}."
            );

            return Ok(new
            {
                message =
                    "PC assigned successfully.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                status =
                    pc.Status,
                userId =
                    user.UserId,
                username =
                    user.Username,
                laboratoryId =
                    pc.LaboratoryId,
                lastSeen =
                    pc.LastSeen,
                isEnabled =
                    pc.IsEnabled
            });
        }

        // ==========================================
        // RELEASE PC
        // ==========================================

        [HttpPost("release/{userId}")]
        public async Task<IActionResult> ReleasePC(
            int userId)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.CurrentUserId == userId);

            if (pc == null)
            {
                return NotFound(new
                {
                    message =
                        "No PC is currently assigned to this user."
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId);

            string pcNumber =
                pc.PCNumber;

            string username =
                user?.Username ??
                $"User {userId}";

            pc.Status =
                "Available";

            pc.CurrentUserId =
                null;

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                userId,
                pc.PCId,
                "PC Released",
                $"PC {pcNumber} released by user {username}."
            );

            return Ok(new
            {
                message =
                    "PC released successfully.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pcNumber,
                status =
                    pc.Status,
                currentUserId =
                    (int?)null,
                laboratoryId =
                    pc.LaboratoryId,
                lastSeen =
                    pc.LastSeen,
                isEnabled =
                    pc.IsEnabled
            });
        }

        // ==========================================
        // HEARTBEAT
        // ==========================================

        [HttpPost("{id}/heartbeat")]
        public async Task<IActionResult> Heartbeat(
            int id)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message =
                        "PC not found."
                });
            }

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Heartbeat received.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                laboratoryId =
                    pc.LaboratoryId,
                status =
                    pc.Status,
                lastSeen =
                    pc.LastSeen,
                isEnabled =
                    pc.IsEnabled
            });
        }

        // ==========================================
        // ENABLE / DISABLE PC
        // ==========================================

        [HttpPut("{id}/enabled")]
        public async Task<IActionResult> UpdateEnabled(
            int id,
            [FromBody] PCEnabledRequest request)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            if (!request.IsEnabled &&
                pc.Status == "Occupied" &&
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        "Cannot disable a PC while it is occupied."
                });
            }

            pc.IsEnabled =
                request.IsEnabled;

            if (!pc.IsEnabled &&
                pc.Status == "Available")
            {
                pc.Status =
                    "Maintenance";
            }

            if (pc.IsEnabled &&
                pc.Status == "Maintenance" &&
                pc.CurrentUserId == null)
            {
                pc.Status =
                    "Available";
            }

            if (pc.IsEnabled)
            {
                pc.MaintenanceReason =
                    null;

                pc.MaintenanceStarted =
                    null;
            }

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            string action =
                pc.IsEnabled
                    ? "PC Enabled"
                    : "PC Disabled";

            string details =
                pc.IsEnabled
                    ? $"PC {pc.PCNumber} was enabled."
                    : $"PC {pc.PCNumber} was disabled.";

            await LogActivity(
                null,
                pc.PCId,
                action,
                details
            );

            return Ok(new
            {
                message =
                    pc.IsEnabled
                        ? "PC enabled successfully."
                        : "PC disabled successfully.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                laboratoryId =
                    pc.LaboratoryId,
                status =
                    pc.Status,
                isEnabled =
                    pc.IsEnabled,
                maintenanceReason =
                    pc.MaintenanceReason,
                maintenanceStarted =
                    pc.MaintenanceStarted,
                lastSeen =
                    pc.LastSeen
            });
        }

        // ==========================================
        // SET PC TO MAINTENANCE
        // ==========================================

        [HttpPut("{id}/maintenance")]
        public async Task<IActionResult> SetMaintenance(
            int id,
            [FromBody] PCMaintenanceRequest request)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            if (pc.Status == "Occupied" &&
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        "Cannot put a PC into maintenance while it is occupied."
                });
            }

            if (string.IsNullOrWhiteSpace(
                request.Reason))
            {
                return BadRequest(new
                {
                    message =
                        "Maintenance reason is required."
                });
            }

            pc.Status =
                "Maintenance";

            pc.IsEnabled =
                false;

            pc.CurrentUserId =
                null;

            pc.MaintenanceReason =
                request.Reason.Trim();

            pc.MaintenanceStarted =
                DateTime.Now;

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "Maintenance Started",
                $"PC {pc.PCNumber} placed under maintenance. Reason: {pc.MaintenanceReason}"
            );

            return Ok(new
            {
                message =
                    "PC placed under maintenance.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                laboratoryId =
                    pc.LaboratoryId,
                status =
                    pc.Status,
                isEnabled =
                    pc.IsEnabled,
                maintenanceReason =
                    pc.MaintenanceReason,
                maintenanceStarted =
                    pc.MaintenanceStarted,
                lastSeen =
                    pc.LastSeen
            });
        }

        // ==========================================
        // CLEAR MAINTENANCE
        // ==========================================

        [HttpPut("{id}/maintenance/clear")]
        public async Task<IActionResult> ClearMaintenance(
            int id)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            pc.Status =
                "Available";

            pc.IsEnabled =
                true;

            pc.CurrentUserId =
                null;

            pc.MaintenanceReason =
                null;

            pc.MaintenanceStarted =
                null;

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "Maintenance Cleared",
                $"Maintenance cleared for PC {pc.PCNumber}. PC is now available."
            );

            return Ok(new
            {
                message =
                    "PC maintenance cleared.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                laboratoryId =
                    pc.LaboratoryId,
                status =
                    pc.Status,
                isEnabled =
                    pc.IsEnabled,
                maintenanceReason =
                    pc.MaintenanceReason,
                maintenanceStarted =
                    pc.MaintenanceStarted,
                lastSeen =
                    pc.LastSeen
            });
        }

        // ==========================================
        // MANUAL STATUS UPDATE
        // ==========================================

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] PCStatusRequest request)
        {
            var pc = await _context.PCs
                .FirstOrDefaultAsync(p =>
                    p.PCId == id);

            if (pc == null)
            {
                return NotFound(new
                {
                    message = "PC not found."
                });
            }

            if (string.IsNullOrWhiteSpace(
                request.Status))
            {
                return BadRequest(new
                {
                    message = "PC status is required."
                });
            }

            pc.Status =
                request.Status.Trim();

            pc.LastSeen =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await LogActivity(
                null,
                pc.PCId,
                "PC Status Updated",
                $"PC {pc.PCNumber} status changed to {pc.Status}."
            );

            return Ok(new
            {
                message =
                    "PC status updated successfully.",
                pcId =
                    pc.PCId,
                pcNumber =
                    pc.PCNumber,
                laboratoryId =
                    pc.LaboratoryId,
                status =
                    pc.Status,
                lastSeen =
                    pc.LastSeen,
                isEnabled =
                    pc.IsEnabled
            });
        }
    }

    // ==========================================
    // ADD PC REQUEST
    // ==========================================

    public class PCCreateRequest
    {
        public string PCNumber { get; set; } =
            string.Empty;

        public int? LaboratoryId { get; set; }

        public string? MACAddress { get; set; }

        public string? IPAddress { get; set; }
    }

    // ==========================================
    // UPDATE PC NUMBER REQUEST
    // ==========================================

    public class PCNumberUpdateRequest
    {
        public string PCNumber { get; set; } =
            string.Empty;
    }

    // ==========================================
    // PC LABORATORY REQUEST
    // ==========================================

    public class PCLaboratoryRequest
    {
        public int? LaboratoryId { get; set; }
    }

    // ==========================================
    // PC NETWORK IDENTITY REQUEST
    // ==========================================

    public class PCNetworkIdentityRequest
    {
        public string? MACAddress { get; set; }

        public string? IPAddress { get; set; }
    }

    // ==========================================
    // PC STATUS REQUEST
    // ==========================================

    public class PCStatusRequest
    {
        public string Status { get; set; } =
            string.Empty;
    }

    // ==========================================
    // ENABLE / DISABLE REQUEST
    // ==========================================

    public class PCEnabledRequest
    {
        public bool IsEnabled { get; set; }
    }

    // ==========================================
    // MAINTENANCE REQUEST
    // ==========================================

    public class PCMaintenanceRequest
    {
        public string Reason { get; set; } =
            string.Empty;
    }
}
