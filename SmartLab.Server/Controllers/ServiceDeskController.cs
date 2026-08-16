using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceDeskController : ControllerBase
    {
        private readonly AppDbContext _context;


        public ServiceDeskController(
            AppDbContext context)
        {
            _context = context;
        }


        // ==========================================
        // GET ALL SERVICE DESK TICKETS
        //
        // GET:
        // api/ServiceDesk
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status = null)
        {
            var query =
                _context.ServiceDeskTickets
                    .AsNoTracking()
                    .AsQueryable();


            // Optional status filter

            if (!string.IsNullOrWhiteSpace(status))
            {
                query =
                    query.Where(t =>
                        t.Status == status);
            }


            var tickets =
                await query
                    .OrderByDescending(
                        t => t.CreatedAt)
                    .ToListAsync();


            return Ok(tickets);
        }


        // ==========================================
        // GET TICKETS FOR ONE TEACHER
        //
        // GET:
        // api/ServiceDesk/teacher/{teacherUserId}
        // ==========================================

        [HttpGet("teacher/{teacherUserId}")]
        public async Task<IActionResult> GetTeacherTickets(
            int teacherUserId)
        {
            var tickets =
                await _context.ServiceDeskTickets
                    .AsNoTracking()
                    .Where(t =>
                        t.TeacherUserId ==
                        teacherUserId)
                    .OrderByDescending(
                        t => t.CreatedAt)
                    .ToListAsync();


            return Ok(tickets);
        }


        // ==========================================
        // GET ONE TICKET
        //
        // GET:
        // api/ServiceDesk/{id}
        // ==========================================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTicket(
            int id)
        {
            var ticket =
                await _context.ServiceDeskTickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t =>
                            t.ServiceDeskTicketId ==
                            id);


            if (ticket == null)
            {
                return NotFound(new
                {
                    message =
                        "Service Desk ticket not found."
                });
            }


            return Ok(ticket);
        }


        // ==========================================
        // CREATE SERVICE DESK TICKET
        //
        // POST:
        // api/ServiceDesk
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CreateTicket(
            [FromBody]
            ServiceDeskCreateRequest request)
        {
            // ==========================================
            // VALIDATION
            // ==========================================

            if (request.TeacherUserId <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "Teacher User ID is required."
                });
            }


            if (string.IsNullOrWhiteSpace(
                request.TeacherUsername))
            {
                return BadRequest(new
                {
                    message =
                        "Teacher username is required."
                });
            }


            if (string.IsNullOrWhiteSpace(
                request.Category))
            {
                return BadRequest(new
                {
                    message =
                        "Assistance category is required."
                });
            }


            if (string.IsNullOrWhiteSpace(
                request.Subject))
            {
                return BadRequest(new
                {
                    message =
                        "Subject is required."
                });
            }


            if (string.IsNullOrWhiteSpace(
                request.Description))
            {
                return BadRequest(new
                {
                    message =
                        "Description is required."
                });
            }


            // ==========================================
            // CREATE TICKET
            // ==========================================

            var ticket =
                new ServiceDeskTicket
                {
                    TeacherUserId =
                        request.TeacherUserId,

                    TeacherUsername =
                        request.TeacherUsername.Trim(),

                    PCNumber =
                        string.IsNullOrWhiteSpace(
                            request.PCNumber)
                            ? null
                            : request.PCNumber.Trim(),

                    Location =
                        string.IsNullOrWhiteSpace(
                            request.Location)
                            ? null
                            : request.Location.Trim(),

                    Category =
                        request.Category.Trim(),

                    Subject =
                        request.Subject.Trim(),

                    Description =
                        request.Description.Trim(),

                    Status = "Open",

                    CreatedAt =
                        DateTime.Now
                };


            _context.ServiceDeskTickets.Add(
                ticket);


            await _context.SaveChangesAsync();


            return CreatedAtAction(
                nameof(GetTicket),
                new
                {
                    id =
                        ticket.ServiceDeskTicketId
                },
                ticket);
        }


        // ==========================================
        // UPDATE TICKET STATUS
        //
        // PUT:
        // api/ServiceDesk/{id}/status
        // ==========================================

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody]
            ServiceDeskStatusUpdateRequest request)
        {
            var ticket =
                await _context.ServiceDeskTickets
                    .FirstOrDefaultAsync(
                        t =>
                            t.ServiceDeskTicketId ==
                            id);


            if (ticket == null)
            {
                return NotFound(new
                {
                    message =
                        "Service Desk ticket not found."
                });
            }


            // ==========================================
            // VALID STATUS
            // ==========================================

            string[] validStatuses =
            {
                "Open",
                "In Progress",
                "Resolved",
                "Closed"
            };


            if (!validStatuses.Contains(
                request.Status))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid ticket status."
                });
            }


            // ==========================================
            // UPDATE STATUS
            // ==========================================

            ticket.Status =
                request.Status;


            // ==========================================
            // ASSIGN MIS STAFF
            // ==========================================

            if (request.AssignedToUserId.HasValue)
            {
                ticket.AssignedToUserId =
                    request.AssignedToUserId;
            }


            if (!string.IsNullOrWhiteSpace(
                request.AssignedToUsername))
            {
                ticket.AssignedToUsername =
                    request.AssignedToUsername.Trim();
            }


            // ==========================================
            // STARTED
            // ==========================================

            if (
                request.Status ==
                    "In Progress" &&
                ticket.StartedAt == null)
            {
                ticket.StartedAt =
                    DateTime.Now;
            }


            // ==========================================
            // RESOLVED
            // ==========================================

            if (
                request.Status ==
                    "Resolved" &&
                ticket.ResolvedAt == null)
            {
                ticket.ResolvedAt =
                    DateTime.Now;
            }


            // ==========================================
            // CLOSED
            // ==========================================

            if (
                request.Status ==
                    "Closed" &&
                ticket.ResolvedAt == null)
            {
                ticket.ResolvedAt =
                    DateTime.Now;
            }


            // ==========================================
            // RESOLUTION NOTES
            // ==========================================

            if (request.ResolutionNotes != null)
            {
                ticket.ResolutionNotes =
                    request.ResolutionNotes.Trim();
            }


            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Service Desk ticket updated.",
                ticket
            });
        }


        // ==========================================
        // DELETE TICKET
        //
        // DELETE:
        // api/ServiceDesk/{id}
        // ==========================================

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicket(
            int id)
        {
            var ticket =
                await _context.ServiceDeskTickets
                    .FirstOrDefaultAsync(
                        t =>
                            t.ServiceDeskTicketId ==
                            id);


            if (ticket == null)
            {
                return NotFound(new
                {
                    message =
                        "Service Desk ticket not found."
                });
            }


            _context.ServiceDeskTickets.Remove(
                ticket);


            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Service Desk ticket deleted.",
                id
            });
        }
    }


    // ==========================================
    // CREATE REQUEST
    // ==========================================

    public class ServiceDeskCreateRequest
    {
        public int TeacherUserId { get; set; }


        public string TeacherUsername { get; set; } =
            string.Empty;


        public string? PCNumber { get; set; }


        public string? Location { get; set; }


        public string Category { get; set; } =
            string.Empty;


        public string Subject { get; set; } =
            string.Empty;


        public string Description { get; set; } =
            string.Empty;
    }


    // ==========================================
    // STATUS UPDATE REQUEST
    // ==========================================

    public class ServiceDeskStatusUpdateRequest
    {
        public string Status { get; set; } =
            string.Empty;


        public int? AssignedToUserId { get; set; }


        public string? AssignedToUsername { get; set; }


        public string? ResolutionNotes { get; set; }
    }
}