using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Student")]
    public class PCRegistrationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PCRegistrationController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // REGISTER / UPDATE THIS STUDENT'S PC
        // ==========================================
        //
        // Security rule:
        // The authenticated Student can only update
        // the PC that is currently assigned to their
        // user account.
        //
        // This prevents a Student from choosing an
        // arbitrary PCId and changing another PC's
        // MAC/IP identity.
        // ==========================================

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] PCRegistrationRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid user ID."
                });
            }

            // ------------------------------------------
            // Make sure the authenticated token belongs
            // to the user ID being submitted.
            // ------------------------------------------

            string? claimUserId =
                User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)
                ?.Value;

            if (!int.TryParse(
                    claimUserId,
                    out int authenticatedUserId))
            {
                return Unauthorized(new
                {
                    message =
                        "Authenticated user ID is missing."
                });
            }

            if (authenticatedUserId != request.UserId)
            {
                return Forbid();
            }

            // ------------------------------------------
            // Validate required PC identity
            // ------------------------------------------

            string pcNumber =
                request.PCNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pcNumber))
            {
                return BadRequest(new
                {
                    message = "PC number is required."
                });
            }

            if (string.IsNullOrWhiteSpace(request.MACAddress))
            {
                return BadRequest(new
                {
                    message = "MAC address is required."
                });
            }

            string macAddress =
                request.MACAddress.Trim();

            string? ipAddress =
                string.IsNullOrWhiteSpace(request.IPAddress)
                    ? null
                    : request.IPAddress.Trim();

            // ------------------------------------------
            // Find the currently assigned PC
            // ------------------------------------------

            var pc =
                await _context.PCs
                    .FirstOrDefaultAsync(p =>
                        p.PCNumber == pcNumber &&
                        p.CurrentUserId == authenticatedUserId);

            if (pc == null)
            {
                return Conflict(new
                {
                    message =
                        "The supplied PC is not currently assigned to the authenticated student."
                });
            }

            // ------------------------------------------
            // Prevent the same MAC from being registered
            // to a different PC.
            // ------------------------------------------

            var duplicateMac =
                await _context.PCs
                    .FirstOrDefaultAsync(p =>
                        p.PCId != pc.PCId &&
                        p.MACAddress != null &&
                        p.MACAddress.ToLower() ==
                        macAddress.ToLower());

            if (duplicateMac != null)
            {
                return Conflict(new
                {
                    message =
                        $"MAC address {macAddress} is already registered to another PC."
                });
            }

            // ------------------------------------------
            // Update network identity
            // ------------------------------------------

            pc.MACAddress = macAddress;
            pc.IPAddress = ipAddress;
            pc.LastSeen = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "PC network identity registered successfully.",
                pcId = pc.PCId,
                pcNumber = pc.PCNumber,
                laboratoryId = pc.LaboratoryId,
                macAddress = pc.MACAddress,
                ipAddress = pc.IPAddress,
                lastSeen = pc.LastSeen
            });
        }
    }

    public class PCRegistrationRequest
    {
        public int UserId { get; set; }

        public string PCNumber { get; set; } =
            string.Empty;

        public string MACAddress { get; set; } =
            string.Empty;

        public string? IPAddress { get; set; }
    }
}
