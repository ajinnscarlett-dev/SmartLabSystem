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

            var existingPC =
                await _context.PCs
                    .FirstOrDefaultAsync(p =>
                        p.PCNumber.ToLower() == pcNumber.ToLower());

            if (existingPC != null)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pcNumber} is already registered."
                });
            }

            var pc = new PC
            {
                PCNumber = pcNumber,
                Status = "Available",
                CurrentUserId = null,
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
                        p.PCNumber.ToLower() == newPcNumber.ToLower());

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
        // LOGIN TO THIS PC
        // ==========================================

        [HttpPost("login/{pcNumber}/{userId}")]
        public async Task<IActionResult> LoginToPC(
            string pcNumber,
            int userId)
        {
            // ==========================================
            // FIND USER
            // ==========================================

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


            // ==========================================
            // FIND THIS PC
            // ==========================================

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


            // ==========================================
            // CHECK IF PC IS ENABLED
            // ==========================================

            if (!pc.IsEnabled)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is currently disabled."
                });
            }


            // ==========================================
            // CHECK MAINTENANCE
            // ==========================================

            if (pc.Status == "Maintenance")
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is currently under maintenance."
                });
            }


            // ==========================================
            // CHECK IF PC IS ALREADY OCCUPIED
            // ==========================================

            if (pc.Status == "Occupied" ||
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is already occupied."
                });
            }


            // ==========================================
            // OCCUPY THIS PC
            // ==========================================

            pc.Status = "Occupied";

            pc.CurrentUserId = userId;

            pc.LastSeen = DateTime.Now;


            await _context.SaveChangesAsync();


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

            await LogActivity(
                userId,
                pc.PCId,
                "PC Login",
                $"User {user.Username} logged in to PC {pc.PCNumber}."
            );


            // ==========================================
            // RETURN PC INFORMATION
            // ==========================================

            return Ok(new
            {
                message = "PC login successful.",

                pcId = pc.PCId,

                pcNumber = pc.PCNumber,

                status = pc.Status,

                userId = user.UserId,

                username = user.Username,

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


            // ==========================================
            // CHECK IF STUDENT ALREADY HAS A PC
            // ==========================================

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
                        existingPC.MaintenanceReason
                });
            }


            // ==========================================
            // FIND AVAILABLE + ENABLED PC
            // ==========================================

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


            // ==========================================
            // ASSIGN PC
            // ==========================================

            pc.Status =
                "Occupied";

            pc.CurrentUserId =
                userId;

            pc.LastSeen =
                DateTime.Now;


            await _context.SaveChangesAsync();


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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


            // ==========================================
            // GET USER INFORMATION
            // ==========================================

            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId);


            string pcNumber =
                pc.PCNumber;

            string username =
                user?.Username ??
                $"User {userId}";


            // ==========================================
            // RELEASE PC
            // ==========================================

            pc.Status =
                "Available";

            pc.CurrentUserId =
                null;

            pc.LastSeen =
                DateTime.Now;


            await _context.SaveChangesAsync();


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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


            // ==========================================
            // UPDATE LAST SEEN
            // ==========================================

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
                    message =
                        "PC not found."
                });
            }


            // ==========================================
            // CANNOT DISABLE OCCUPIED PC
            // ==========================================

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


            // ==========================================
            // DISABLE AVAILABLE PC
            // ==========================================

            if (!pc.IsEnabled &&
                pc.Status == "Available")
            {
                pc.Status =
                    "Maintenance";
            }


            // ==========================================
            // ENABLE MAINTENANCE PC
            // ==========================================

            if (pc.IsEnabled &&
                pc.Status == "Maintenance" &&
                pc.CurrentUserId == null)
            {
                pc.Status =
                    "Available";
            }


            // ==========================================
            // CLEAR MAINTENANCE DATA WHEN ENABLED
            // ==========================================

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


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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
                    message =
                        "PC not found."
                });
            }


            // ==========================================
            // CANNOT PUT OCCUPIED PC INTO MAINTENANCE
            // ==========================================

            if (pc.Status == "Occupied" &&
                pc.CurrentUserId != null)
            {
                return Conflict(new
                {
                    message =
                        "Cannot put a PC into maintenance while it is occupied."
                });
            }


            // ==========================================
            // VALIDATE REASON
            // ==========================================

            if (string.IsNullOrWhiteSpace(
                request.Reason))
            {
                return BadRequest(new
                {
                    message =
                        "Maintenance reason is required."
                });
            }


            // ==========================================
            // SET MAINTENANCE
            // ==========================================

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


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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
                    message =
                        "PC not found."
                });
            }


            // ==========================================
            // CLEAR MAINTENANCE
            // ==========================================

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


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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
                    message =
                        "PC not found."
                });
            }


            pc.Status =
                request.Status;

            pc.LastSeen =
                DateTime.Now;


            await _context.SaveChangesAsync();


            // ==========================================
            // ACTIVITY LOG
            // ==========================================

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