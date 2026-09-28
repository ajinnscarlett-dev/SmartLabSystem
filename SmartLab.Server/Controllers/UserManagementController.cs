using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers;

[ApiController]
[Route("api/User")]
[Authorize(Roles = "Admin")]
public class UserManagementController : ControllerBase
{
    private const string DisabledPrefix = "__SMARTLAB_DISABLED__|";
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _hasher = new();

    public UserManagementController(AppDbContext context) => _context = context;

    [HttpGet("management")]
    public async Task<IActionResult> GetAccounts()
    {
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        return Ok(users.Select(u => new
        {
            userId = u.UserId,
            username = DisplayUsername(u.Username),
            studentNumber = u.StudentNumber,
            fullName = u.FullName,
            role = DisplayRole(u.Role),
            status = IsDisabled(u) ? "Inactive" : "Active",
            mustChangePassword = u.MustChangePassword,
            createdAt = u.CreatedAt
        }));
    }

    [HttpPost("management")]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
    {
        string username = request.Username?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;
        string? role = NormalizeRole(request.Role);

        if (string.IsNullOrWhiteSpace(username)) return BadRequest(new { message = "Username is required." });
        if (password.Length < 6) return BadRequest(new { message = "Password must be at least 6 characters." });
        if (role == null) return BadRequest(new { message = "Role must be Student, Teacher, or Admin." });
        if (await _context.Users.AnyAsync(u => !u.Username.StartsWith(DisabledPrefix) && u.Username.ToLower() == username.ToLower()))
            return Conflict(new { message = $"Username '{username}' already exists." });

        var user = new User
        {
            Username = username,
            StudentNumber = role == "Student" ? username : null,
            Role = role,
            MustChangePassword = true,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await LogAsync(user.UserId, "User Added", $"User {username} created with role {role}.");
        return Ok(new { message = "User created successfully.", userId = user.UserId, mustChangePassword = user.MustChangePassword });
    }

    [HttpPost("management/students/import")]
    public async Task<IActionResult> ImportStudents([FromBody] StudentImportBatchRequest request)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { message = "At least one student row is required." });

        if (request.Rows.Count > 2000)
            return BadRequest(new { message = "A single import is limited to 2,000 students." });

        string[] incomingNumbers = request.Rows
            .Select(r => r.StudentNumber?.Trim() ?? string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        HashSet<string> existingNumbers = new(
            await _context.Users.AsNoTracking()
                .Where(u => u.StudentNumber != null && incomingNumbers.Contains(u.StudentNumber!))
                .Select(u => u.StudentNumber!)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        HashSet<string> existingUsernames = new(
            await _context.Users.AsNoTracking()
                .Where(u => incomingNumbers.Contains(u.Username))
                .Select(u => u.Username)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        HashSet<string> seenNumbers = new(StringComparer.OrdinalIgnoreCase);
        List<StudentImportRejectedRow> rejected = new();
        List<User> importedUsers = new();

        foreach (StudentImportRowRequest row in request.Rows)
        {
            string studentNumber = row.StudentNumber?.Trim() ?? string.Empty;
            string studentName = row.StudentName?.Trim() ?? string.Empty;
            int rowNumber = row.RowNumber > 0 ? row.RowNumber : 0;

            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "Missing Student Number."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(studentName))
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "Missing Student Name."));
                continue;
            }

            if (studentNumber.Length > 80)
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "Student Number exceeds 80 characters."));
                continue;
            }

            if (studentName.Length > 200)
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "Student Name exceeds 200 characters."));
                continue;
            }

            if (!seenNumbers.Add(studentNumber))
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "Duplicate Student Number in this import."));
                continue;
            }

            if (existingNumbers.Contains(studentNumber) || existingUsernames.Contains(studentNumber))
            {
                rejected.Add(new StudentImportRejectedRow(rowNumber, studentNumber, studentName, "A SmartLab account already uses this Student Number."));
                continue;
            }

            User user = new()
            {
                Username = studentNumber,
                StudentNumber = studentNumber,
                FullName = studentName,
                Role = "Student",
                MustChangePassword = true,
                CreatedAt = DateTime.Now
            };

            user.PasswordHash = _hasher.HashPassword(user, studentNumber);
            importedUsers.Add(user);
            existingNumbers.Add(studentNumber);
            existingUsernames.Add(studentNumber);
        }

        if (importedUsers.Count > 0)
        {
            _context.Users.AddRange(importedUsers);
            await _context.SaveChangesAsync();

            foreach (User user in importedUsers)
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Student Imported",
                    Details = $"Student {user.StudentNumber} ({user.FullName}) was imported. Initial password was assigned as the Student Number and must be changed at first login.",
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = importedUsers.Count == 0
                ? "No student records were imported."
                : "Student import completed.",
            importedCount = importedUsers.Count,
            rejectedCount = rejected.Count,
            imported = importedUsers.Select(u => new
            {
                userId = u.UserId,
                studentNumber = u.StudentNumber,
                fullName = u.FullName,
                mustChangePassword = u.MustChangePassword
            }),
            rejected
        });
    }

    [HttpPut("management/{id}/username")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameRequest request)
    {
        string username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username)) return BadRequest(new { message = "Username is required." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound(new { message = "User not found." });
        if (await _context.Users.AnyAsync(u => u.UserId != id && !u.Username.StartsWith(DisabledPrefix) && u.Username.ToLower() == username.ToLower()))
            return Conflict(new { message = $"Username '{username}' already exists." });

        string old = DisplayUsername(user.Username);
        user.Username = IsDisabled(user) ? PackDisabledUsername(username) : username;
        if (string.Equals(DisplayRole(user.Role), "Student", StringComparison.OrdinalIgnoreCase) && !IsDisabled(user))
            user.StudentNumber ??= username;
        await _context.SaveChangesAsync();
        await LogAsync(id, "Username Changed", $"Username changed from {old} to {username}.");
        return Ok(new { message = "Username updated successfully." });
    }

    [HttpPut("management/{id}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
    {
        string? role = NormalizeRole(request.Role);
        if (role == null) return BadRequest(new { message = "Role must be Student, Teacher, or Admin." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound(new { message = "User not found." });
        if (IsDisabled(user)) return BadRequest(new { message = "Restore the account before changing its role." });

        int currentId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsed) ? parsed : -1;
        if (currentId == id && role != "Admin") return BadRequest(new { message = "You cannot remove Admin access from your current account." });
        if (user.Role == "Admin" && role != "Admin" && await _context.Users.CountAsync(u => u.Role == "Admin" && !u.Username.StartsWith(DisabledPrefix)) <= 1)
            return BadRequest(new { message = "The last active Admin cannot be changed to a non-Admin role." });

        string old = user.Role;
        user.Role = role;
        if (role == "Student" && string.IsNullOrWhiteSpace(user.StudentNumber))
            user.StudentNumber = DisplayUsername(user.Username);
        await _context.SaveChangesAsync();
        await LogAsync(id, "User Role Changed", $"Role changed from {old} to {role}.");
        return Ok(new { message = "User role updated successfully." });
    }

    [HttpPut("management/{id}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] PasswordResetRequest request)
    {
        string newPassword = request.NewPassword ?? string.Empty;
        if (newPassword.Length < 6) return BadRequest(new { message = "Password must be at least 6 characters." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound(new { message = "User not found." });

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.MustChangePassword = true;
        await _context.SaveChangesAsync();
        await LogAsync(id, "Password Reset", $"Password reset for user {DisplayUsername(user.Username)}; next login requires a password change.");
        return Ok(new { message = "Password reset successfully.", mustChangePassword = true });
    }

    [HttpDelete("management/{id}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound(new { message = "User not found." });
        if (IsDisabled(user)) return BadRequest(new { message = "Account is already inactive." });

        int currentId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsed) ? parsed : -1;
        if (currentId == id) return BadRequest(new { message = "You cannot deactivate the account currently being used." });
        if (user.Role == "Admin" && await _context.Users.CountAsync(u => u.Role == "Admin" && !u.Username.StartsWith(DisabledPrefix)) <= 1)
            return BadRequest(new { message = "The last active Admin cannot be deactivated." });

        string username = DisplayUsername(user.Username);
        user.Username = PackDisabledUsername(username);
        user.Role = "Disabled:" + DisplayRole(user.Role);
        await _context.SaveChangesAsync();
        await LogAsync(id, "User Deactivated", $"User {username} was deactivated.");
        return Ok(new { message = "User account deactivated successfully." });
    }

    [HttpPost("management/{id}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound(new { message = "User not found." });
        if (!IsDisabled(user)) return BadRequest(new { message = "Account is already active." });

        string username = DisplayUsername(user.Username);
        string role = DisplayRole(user.Role);
        if (await _context.Users.AnyAsync(u => u.UserId != id && !u.Username.StartsWith(DisabledPrefix) && u.Username.ToLower() == username.ToLower()))
            return Conflict(new { message = $"Username '{username}' is already in use." });

        user.Username = username;
        user.Role = role;
        await _context.SaveChangesAsync();
        await LogAsync(id, "User Restored", $"User {username} was restored.");
        return Ok(new { message = "User account restored successfully." });
    }

    private async Task LogAsync(int userId, string action, string details)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            PCId = null,
            Action = action,
            Details = details,
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();
    }

    private static string? NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "student" => "Student",
        "teacher" => "Teacher",
        "admin" => "Admin",
        _ => null
    };

    private static bool IsDisabled(User user) =>
        user.Username.StartsWith(DisabledPrefix, StringComparison.Ordinal) ||
        user.Role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase);

    private static string DisplayUsername(string username)
    {
        if (!username.StartsWith(DisabledPrefix, StringComparison.Ordinal)) return username;
        string payload = username[DisabledPrefix.Length..];
        int separator = payload.IndexOf('|');
        if (separator <= 0) return username;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(payload[..separator])); }
        catch { return username; }
    }

    private static string DisplayRole(string role) => role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase) ? role[9..] : role;

    private static string PackDisabledUsername(string username) =>
        DisabledPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(username)) + "|" + Guid.NewGuid().ToString("N");

    public sealed class CreateAccountRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";
    }

    public sealed class RenameRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    public sealed class ChangeRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public sealed class PasswordResetRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public sealed class StudentImportBatchRequest
    {
        public List<StudentImportRowRequest> Rows { get; set; } = new();
    }

    public sealed class StudentImportRowRequest
    {
        public int RowNumber { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
    }

    public sealed record StudentImportRejectedRow(
        int RowNumber,
        string StudentNumber,
        string StudentName,
        string Reason);
}
