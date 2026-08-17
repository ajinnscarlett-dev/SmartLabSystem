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

        private static readonly ConcurrentDictionary<long, PcCommand> Commands = new();
        private static readonly ConcurrentDictionary<int, long> PendingCommandByPc = new();

        public PCCommandController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{pcId}/send-message")]
        public async Task<IActionResult> SendMessage(
            int pcId,
            [FromBody] SendMessageRequest request)
        {
            string message = request.Message?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(message))
                return BadRequest(new { message = "Message is required." });

            if (message.Length > 1000)
                return BadRequest(new { message = "Message cannot exceed 1000 characters." });

            PC? pc = await _context.PCs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PCId == pcId);

            if (pc == null)
                return NotFound(new { message = "PC not found." });

            if (!pc.IsEnabled)
                return Conflict(new { message = $"PC {pc.PCNumber} is disabled." });

            bool occupied =
                pc.Status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) ||
                pc.Status.Equals("In Use", StringComparison.OrdinalIgnoreCase);

            if (!occupied || !pc.CurrentUserId.HasValue)
                return Conflict(new
                {
                    message = "A student must be logged in to this PC before a message can be sent."
                });

            if (PendingCommandByPc.ContainsKey(pcId))
                return Conflict(new
                {
                    message = "There is already a pending command for this PC."
                });

            string? claimUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(claimUserId, out int requestedByUserId))
                return Unauthorized(new
                {
                    message = "Unable to determine the requesting user."
                });

            long commandId = Interlocked.Increment(ref _nextCommandId);

            PcCommand command = new PcCommand
            {
                CommandId = commandId,
                PCId = pcId,
                RequestedByUserId = requestedByUserId,
                CommandType = "SEND_MESSAGE",
                Message = message,
                CreatedAt = DateTime.Now,
                Status = "Pending"
            };

            Commands[commandId] = command;

            if (!PendingCommandByPc.TryAdd(pcId, commandId))
            {
                Commands.TryRemove(commandId, out _);
                return Conflict(new
                {
                    message = "Another command is already pending for this PC."
                });
            }

            await LogActivityAsync(
                requestedByUserId,
                pcId,
                "PC Command Sent",
                $"SEND_MESSAGE command #{commandId} was sent to PC {pc.PCNumber}."
            );

            return Ok(new
            {
                message = "Message command queued successfully.",
                commandId,
                pcId,
                pcNumber = pc.PCNumber,
                commandType = command.CommandType,
                status = command.Status,
                createdAt = command.CreatedAt
            });
        }

        [Authorize(Roles = "Student")]
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingCommand()
        {
            string? claimUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(claimUserId, out int userId))
                return Unauthorized();

            PC? pc = await _context.PCs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.CurrentUserId == userId && p.IsEnabled);

            if (pc == null)
                return NoContent();

            if (!PendingCommandByPc.TryGetValue(pc.PCId, out long commandId))
                return NoContent();

            if (!Commands.TryGetValue(commandId, out PcCommand? command))
            {
                PendingCommandByPc.TryRemove(pc.PCId, out _);
                return NoContent();
            }

            if (!command.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return NoContent();

            if (command.PCId != pc.PCId)
                return NoContent();

            return Ok(new
            {
                commandId = command.CommandId,
                pcId = command.PCId,
                commandType = command.CommandType,
                message = command.Message,
                createdAt = command.CreatedAt
            });
        }

        [Authorize(Roles = "Student")]
        [HttpPost("{commandId}/complete")]
        public async Task<IActionResult> CompleteCommand(
            long commandId,
            [FromBody] CompleteCommandRequest request)
        {
            string? claimUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(claimUserId, out int userId))
                return Unauthorized();

            if (!Commands.TryGetValue(commandId, out PcCommand? command))
                return NotFound(new { message = "Command not found." });

            PC? pc = await _context.PCs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PCId == command.PCId);

            if (pc == null || pc.CurrentUserId != userId)
                return Forbid();

            if (!command.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return Conflict(new
                {
                    message = "Command has already been completed."
                });

            command.CompletedAt = DateTime.Now;
            command.Status = request.Success ? "Completed" : "Failed";
            command.Result = string.IsNullOrWhiteSpace(request.Result)
                ? null
                : request.Result.Trim();

            PendingCommandByPc.TryRemove(command.PCId, out _);

            string resultText = command.Status;
            if (!string.IsNullOrWhiteSpace(command.Result))
                resultText += $" - {command.Result}";

            await LogActivityAsync(
                userId,
                command.PCId,
                "PC Command Result",
                $"Command #{command.CommandId} ({command.CommandType}) result: {resultText}."
            );

            return Ok(new
            {
                message = "Command result recorded.",
                commandId = command.CommandId,
                status = command.Status,
                completedAt = command.CompletedAt
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("{commandId}")]
        public IActionResult GetCommandStatus(long commandId)
        {
            if (!Commands.TryGetValue(commandId, out PcCommand? command))
                return NotFound(new { message = "Command not found." });

            return Ok(new
            {
                commandId = command.CommandId,
                pcId = command.PCId,
                commandType = command.CommandType,
                status = command.Status,
                message = command.Message,
                createdAt = command.CreatedAt,
                completedAt = command.CompletedAt,
                result = command.Result
            });
        }

        private async Task LogActivityAsync(
            int? userId,
            int? pcId,
            string action,
            string details)
        {
            ActivityLog log = new ActivityLog
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
