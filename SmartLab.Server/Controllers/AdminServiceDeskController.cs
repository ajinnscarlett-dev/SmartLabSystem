using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminServiceDeskController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminServiceDeskController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET ACTIVE TEACHER SERVICE DESK TICKETS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetAllTickets()
        {
            var tickets =
                await _context.ServiceDeskTickets
                    .Where(t => t.Status != "Resolved")
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        t.ServiceDeskTicketId,
                        t.TeacherUserId,
                        t.TeacherUsername,
                        t.PCNumber,
                        t.Location,
                        t.Category,
                        t.Subject,
                        t.Description,
                        t.Status,
                        t.AssignedToUserId,
                        t.AssignedToUsername,
                        t.CreatedAt,
                        t.StartedAt,
                        t.ResolvedAt,
                        t.ResolutionNotes
                    })
                    .ToListAsync();

            return Ok(tickets);
        }


        // ==========================================
        // RESPOND / RESOLVE
        // ==========================================

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTicket(
            int id,
            [FromBody] ServiceDeskUpdateRequest request)
        {
            var ticket =
                await _context.ServiceDeskTickets
                    .FirstOrDefaultAsync(t =>
                        t.ServiceDeskTicketId == id);

            if (ticket == null)
            {
                return NotFound(new
                {
                    message =
                        "Service Desk ticket not found."
                });
            }

            if (string.IsNullOrWhiteSpace(
                request.Message))
            {
                return BadRequest(new
                {
                    message =
                        "Message is required."
                });
            }


            // ==========================================
            // STORE MIS RESPONSE
            // ==========================================

            ticket.ResolutionNotes =
                request.Message.Trim();

            ticket.AssignedToUsername =
                string.IsNullOrWhiteSpace(
                    request.AssignedToUsername)
                    ? null
                    : request.AssignedToUsername.Trim();


            // ==========================================
            // RESPOND / IN PROGRESS
            // ==========================================

            if (!request.Resolved)
            {
                ticket.Status = "In Progress";

                if (!ticket.StartedAt.HasValue)
                {
                    ticket.StartedAt =
                        DateTime.Now;
                }

                ticket.ResolvedAt = null;
            }


            // ==========================================
            // RESOLVE
            // ==========================================

            else
            {
                ticket.Status = "Resolved";

                if (!ticket.StartedAt.HasValue)
                {
                    ticket.StartedAt =
                        DateTime.Now;
                }

                ticket.ResolvedAt =
                    DateTime.Now;
            }


            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    request.Resolved
                        ? "Service Desk request resolved."
                        : "Reply sent to teacher.",
                ticketId =
                    ticket.ServiceDeskTicketId,
                status =
                    ticket.Status
            });
        }
    }


    // ==========================================
    // SERVICE DESK UPDATE REQUEST
    // ==========================================

    public class ServiceDeskUpdateRequest
    {
        public string Status { get; set; } =
            "In Progress";

        public string Message { get; set; } =
            string.Empty;

        public string? AssignedToUsername { get; set; }

        public bool Resolved { get; set; }
    }
}