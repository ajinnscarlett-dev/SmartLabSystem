using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PCCommandController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static long _nextCommandId;

        private sealed class PcCommand
        {
            public long CommandId { get; set; }
            public int PCId { get; set; }
            public int RequestedByUserId { get; set; }
            public string CommandType { get; set; } = string.Empty;
            public string? Message { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string Status { get; set; } = "Pending";
            public string? Result { get; set; }
        }

        private static readonly ConcurrentDictionary<long, PcCommand>
            Commands = new();

        private static readonly ConcurrentDictionary<int, long>
            PendingCommandByPc = new();

        public PCCommandController(
            AppDbContext context)
        {
            _context = context;
        }

        // ==========================================================
        // SEND MESSAGE
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/send-message")]
        public async Task<IActionResult> SendMessage(
            int pcId,
            [FromBody] SendMessageRequest request)
        {
            string message =
                request.Message?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new
                {
                    message =
                        "Message is required."
                });
            }

            if (message.Length > 1000)
            {
                return BadRequest(new
                {
                    message =
                        "Message cannot exceed 1000 characters."
                });
            }

            return await QueuePcCommandAsync(
                pcId,
                "SEND_MESSAGE",
                message);
        }

        // ==========================================================
        // LOCK COMPUTER
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/lock")]
        public async Task<IActionResult> LockComputer(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "LOCK_COMPUTER",
                null);
        }

        // ==========================================================
        // BLANK SCREEN
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/blank-screen")]
        public async Task<IActionResult> BlankScreen(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "BLANK_SCREEN",
                null);
        }




        // ==========================================================
        // SHUTDOWN COMPUTER
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/shutdown")]
        public async Task<IActionResult> ShutdownComputer(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "SHUTDOWN_COMPUTER",
                null);
        }

        // ==========================================================
        // LOG OFF USER
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/logoff")]
        public async Task<IActionResult> LogoffUser(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "LOGOFF_USER",
                null);
        }

        // ==========================================================
        // UNBLANK SCREEN
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/unblank-screen")]
        public async Task<IActionResult> UnblankScreen(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "UNBLANK_SCREEN",
                null);
        }

        // ==========================================================
        // UNLOCK REQUEST
        // ==========================================================
        //
        // IMPORTANT:
        // Windows workstation unlock requires local Windows sign-in.
        // SmartLab does not bypass Windows authentication.
        //
        // This command is therefore an "UNLOCK REQUEST": the Student
        // Client receives the request and tells the student to sign in
        // locally.
        //

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/unlock")]
        public async Task<IActionResult> UnlockComputer(
            int pcId)
        {
            return await QueuePcCommandAsync(
                pcId,
                "UNLOCK_REQUEST",
                null);
        }

        // ==========================================================
        // GENERIC COMMAND QUEUE
        // ==========================================================

        private async Task<IActionResult>
            QueuePcCommandAsync(
                int pcId,
                string commandType,
                string? message)
        {
            PC? pc =
                await _context.PCs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p =>
                            p.PCId ==
                            pcId);

            if (pc == null)
            {
                return NotFound(new
                {
                    message =
                        "PC not found."
                });
            }

            if (!pc.IsEnabled)
            {
                return Conflict(new
                {
                    message =
                        $"PC {pc.PCNumber} is disabled."
                });
            }

            bool occupied =
                pc.Status.Equals(
                    "Occupied",
                    StringComparison.OrdinalIgnoreCase)
                ||
                pc.Status.Equals(
                    "In Use",
                    StringComparison.OrdinalIgnoreCase);

            if (!occupied ||
                !pc.CurrentUserId.HasValue)
            {
                return Conflict(new
                {
                    message =
                        "A student must be logged in to this PC."
                });
            }

            // ======================================================
            // SERVER-SIDE TEACHER COMLAB AUTHORIZATION
            // ======================================================

            string? claimUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            if (!int.TryParse(
                claimUserId,
                out int requestedByUserId))
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to determine the requesting user."
                });
            }

            bool isAdmin =
                User.IsInRole("Admin");

            bool isTeacher =
                User.IsInRole("Teacher");

            if (!isAdmin &&
                !isTeacher)
            {
                return Forbid();
            }

            if (isTeacher)
            {
                if (!pc.LaboratoryId.HasValue)
                {
                    return Forbid();
                }

                await EnsureAuthorizationTableAsync();

                bool authorized =
                    await _context
                        .TeacherLaboratoryAuthorizations
                        .AsNoTracking()
                        .AnyAsync(a =>
                            a.TeacherUserId ==
                            requestedByUserId
                            &&
                            a.LaboratoryId ==
                            pc.LaboratoryId.Value);

                if (!authorized)
                {
                    return StatusCode(
                        StatusCodes.Status403Forbidden,
                        new
                        {
                            message =
                                "You are not authorized to control this PC's COMLAB."
                        });
                }
            }

            // ======================================================
            // ONE PENDING COMMAND PER PC
            // ======================================================

            if (PendingCommandByPc.ContainsKey(
                    pcId))
            {
                return Conflict(new
                {
                    message =
                        "There is already a pending command for this PC."
                });
            }

            long commandId =
                Interlocked.Increment(
                    ref _nextCommandId);

            PcCommand command =
                new PcCommand
                {
                    CommandId =
                        commandId,

                    PCId =
                        pcId,

                    RequestedByUserId =
                        requestedByUserId,

                    CommandType =
                        commandType,

                    Message =
                        message,

                    CreatedAt =
                        DateTime.Now,

                    Status =
                        "Pending"
                };

            Commands[commandId] =
                command;

            if (!PendingCommandByPc.TryAdd(
                    pcId,
                    commandId))
            {
                Commands.TryRemove(
                    commandId,
                    out _);

                return Conflict(new
                {
                    message =
                        "Another command is already pending for this PC."
                });
            }

            await LogActivityAsync(
                requestedByUserId,
                pcId,
                "PC Command Sent",
                $"{commandType} command #{commandId} was sent to PC {pc.PCNumber}."
            );

            return Ok(new
            {
                message =
                    $"{commandType} command queued successfully.",

                commandId,

                pcId,

                pcNumber =
                    pc.PCNumber,

                commandType,

                status =
                    command.Status,

                createdAt =
                    command.CreatedAt
            });
        }

        // ==========================================================
        // STUDENT - GET PENDING COMMAND
        // ==========================================================

        [Authorize(Roles = "Student")]
        [HttpGet("pending")]
        public async Task<IActionResult>
            GetPendingCommand()
        {
            string? claimUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            if (!int.TryParse(
                claimUserId,
                out int userId))
            {
                return Unauthorized();
            }

            PC? pc =
                await _context.PCs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p =>
                            p.CurrentUserId ==
                            userId
                            &&
                            p.IsEnabled);

            if (pc == null)
            {
                return NoContent();
            }

            if (!PendingCommandByPc.TryGetValue(
                    pc.PCId,
                    out long commandId))
            {
                return NoContent();
            }

            if (!Commands.TryGetValue(
                    commandId,
                    out PcCommand? command))
            {
                PendingCommandByPc.TryRemove(
                    pc.PCId,
                    out _);

                return NoContent();
            }

            if (!command.Status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return NoContent();
            }

            if (command.PCId !=
                pc.PCId)
            {
                return NoContent();
            }

            return Ok(new
            {
                commandId =
                    command.CommandId,

                pcId =
                    command.PCId,

                commandType =
                    command.CommandType,

                message =
                    command.Message,

                createdAt =
                    command.CreatedAt
            });
        }

        // ==========================================================
        // STUDENT - COMPLETE COMMAND
        // ==========================================================

        [Authorize(Roles = "Student")]
        [HttpPost("{commandId}/complete")]
        public async Task<IActionResult>
            CompleteCommand(
                long commandId,
                [FromBody]
                    CompleteCommandRequest request)
        {
            string? claimUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            if (!int.TryParse(
                claimUserId,
                out int userId))
            {
                return Unauthorized();
            }

            if (!Commands.TryGetValue(
                    commandId,
                    out PcCommand? command))
            {
                return NotFound(new
                {
                    message =
                        "Command not found."
                });
            }

            PC? pc =
                await _context.PCs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p =>
                            p.PCId ==
                            command.PCId);

            if (pc == null ||
                pc.CurrentUserId !=
                    userId)
            {
                return Forbid();
            }

            if (!command.Status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    message =
                        "Command has already been completed."
                });
            }

            command.CompletedAt =
                DateTime.Now;

            command.Status =
                request.Success
                    ? "Completed"
                    : "Failed";

            command.Result =
                string.IsNullOrWhiteSpace(
                    request.Result)
                    ? null
                    : request.Result.Trim();

            PendingCommandByPc.TryRemove(
                command.PCId,
                out _);

            string resultText =
                command.Status;

            if (!string.IsNullOrWhiteSpace(
                command.Result))
            {
                resultText +=
                    $" - {command.Result}";
            }

            await LogActivityAsync(
                userId,
                command.PCId,
                "PC Command Result",
                $"Command #{command.CommandId} ({command.CommandType}) result: {resultText}."
            );

            return Ok(new
            {
                message =
                    "Command result recorded.",

                commandId =
                    command.CommandId,

                status =
                    command.Status,

                completedAt =
                    command.CompletedAt
            });
        }

        // ==========================================================
        // ADMIN / TEACHER - COMMAND STATUS
        // ==========================================================

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{commandId}")]
        public IActionResult GetCommandStatus(
            long commandId)
        {
            if (!Commands.TryGetValue(
                    commandId,
                    out PcCommand? command))
            {
                return NotFound(new
                {
                    message =
                        "Command not found."
                });
            }

            return Ok(new
            {
                commandId =
                    command.CommandId,

                pcId =
                    command.PCId,

                commandType =
                    command.CommandType,

                status =
                    command.Status,

                message =
                    command.Message,

                createdAt =
                    command.CreatedAt,

                completedAt =
                    command.CompletedAt,

                result =
                    command.Result
            });
        }

        // ==========================================================
        // CREATE AUTHORIZATION TABLE IF NEEDED
        // ==========================================================

        private async Task
            EnsureAuthorizationTableAsync()
        {
            await _context.Database
                .ExecuteSqlRawAsync(
                    """
                    IF OBJECT_ID(
                        N'dbo.TeacherLaboratoryAuthorizations',
                        N'U'
                    ) IS NULL
                    BEGIN
                        CREATE TABLE dbo.TeacherLaboratoryAuthorizations
                        (
                            TeacherLaboratoryAuthorizationId INT IDENTITY(1,1)
                                NOT NULL
                                CONSTRAINT PK_TeacherLaboratoryAuthorizations
                                PRIMARY KEY,

                            TeacherUserId INT NOT NULL,

                            LaboratoryId INT NOT NULL,

                            CreatedAt DATETIME2 NOT NULL
                                CONSTRAINT DF_TeacherLaboratoryAuthorizations_CreatedAt
                                DEFAULT(GETDATE()),

                            CONSTRAINT FK_TeacherLaboratoryAuthorizations_User
                                FOREIGN KEY (TeacherUserId)
                                REFERENCES dbo.Users(UserId),

                            CONSTRAINT FK_TeacherLaboratoryAuthorizations_Laboratory
                                FOREIGN KEY (LaboratoryId)
                                REFERENCES dbo.Laboratories(LaboratoryId),

                            CONSTRAINT UQ_TeacherLaboratoryAuthorizations
                                UNIQUE(TeacherUserId, LaboratoryId)
                        );
                    END
                    """);
        }

        // ==========================================================
        // ACTIVITY LOG
        // ==========================================================

        private async Task LogActivityAsync(
            int? userId,
            int? pcId,
            string action,
            string details)
        {
            ActivityLog log =
                new ActivityLog
                {
                    UserId =
                        userId,

                    PCId =
                        pcId,

                    Action =
                        action,

                    Details =
                        details,

                    CreatedAt =
                        DateTime.Now
                };

            _context.ActivityLogs.Add(log);

            await _context.SaveChangesAsync();
        }

        // ==========================================================
        // REQUEST MODELS
        // ==========================================================

        public class SendMessageRequest
        {
            public string? Message { get; set; }
        }

        public class CompleteCommandRequest
        {
            public bool Success { get; set; }

            public string? Result { get; set; }
        }
    }
}
