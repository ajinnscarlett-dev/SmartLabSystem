using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    userId = u.UserId,
                    username = u.Username,
                    role = u.Role,
                    createdAt = u.CreatedAt,
                    mustChangePassword = u.MustChangePassword
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    userId = u.UserId,
                    username = u.Username,
                    role = u.Role,
                    createdAt = u.CreatedAt,
                    mustChangePassword = u.MustChangePassword
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(
            [FromBody] CreateUserRequest request)
        {
            string username =
                request.Username?.Trim() ?? string.Empty;

            string password =
                request.Password ?? string.Empty;

            string role =
                request.Role?.Trim() ?? "Student";

            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new
                {
                    message = "Username is required."
                });
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new
                {
                    message = "Password is required."
                });
            }

            if (password.Length < 6)
            {
                return BadRequest(new
                {
                    message =
                        "Password must be at least 6 characters."
                });
            }

            if (!role.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Role must be Student, Teacher, or Admin."
                });
            }

            if (role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                role = "Admin";
            }
            else if (role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
            {
                role = "Teacher";
            }
            else
            {
                role = "Student";
            }

            bool usernameExists =
                await _context.Users.AnyAsync(
                    u => u.Username.ToLower() ==
                         username.ToLower());

            if (usernameExists)
            {
                return Conflict(new
                {
                    message =
                        $"Username '{username}' already exists."
                });
            }

            var user = new User
            {
                Username = username,
                Role = role,
                CreatedAt = DateTime.Now,
                MustChangePassword = true
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    password);

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            _context.ActivityLogs.Add(
                new ActivityLog
                {
                    PCId = null,
                    UserId = user.UserId,
                    Action = "User Added",
                    Details =
                        $"User {user.Username} was created with role {user.Role}.",
                    CreatedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User added successfully.",
                userId = user.UserId,
                username = user.Username,
                role = user.Role,
                createdAt = user.CreatedAt,
                mustChangePassword = user.MustChangePassword
            });
        }

        [HttpPut("{id}/role")]
        public async Task<IActionResult> ChangeRole(
            int id,
            [FromBody] ChangeRoleRequest request)
        {
            string newRole =
                request.Role?.Trim() ?? string.Empty;

            if (!newRole.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !newRole.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !newRole.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Role must be Student, Teacher, or Admin."
                });
            }

            if (newRole.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                newRole = "Admin";
            }
            else if (newRole.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
            {
                newRole = "Teacher";
            }
            else
            {
                newRole = "Student";
            }

            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            if (user.Role.Equals(
                    newRole,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        $"User is already a {newRole}."
                });
            }

            string oldRole = user.Role;

            user.Role = newRole;

            await _context.SaveChangesAsync();

            _context.ActivityLogs.Add(
                new ActivityLog
                {
                    PCId = null,
                    UserId = user.UserId,
                    Action = "User Role Changed",
                    Details =
                        $"User {user.Username} role changed from {oldRole} to {newRole}.",
                    CreatedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "User role updated successfully.",
                userId = user.UserId,
                username = user.Username,
                role = user.Role,
                mustChangePassword = user.MustChangePassword
            });
        }

        [HttpPut("{id}/password")]
        public async Task<IActionResult> ResetPassword(
            int id,
            [FromBody] ResetPasswordRequest request)
        {
            string newPassword =
                request.NewPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                return BadRequest(new
                {
                    message =
                        "New password is required."
                });
            }

            if (newPassword.Length < 6)
            {
                return BadRequest(new
                {
                    message =
                        "Password must be at least 6 characters."
                });
            }

            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    newPassword);

            user.MustChangePassword = true;

            await _context.SaveChangesAsync();

            _context.ActivityLogs.Add(
                new ActivityLog
                {
                    PCId = null,
                    UserId = user.UserId,
                    Action = "Password Reset",
                    Details =
                        $"Password was reset for user {user.Username}. User must change it before normal operation.",
                    CreatedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Password reset successfully. The user must change the temporary password at next login.",
                userId = user.UserId,
                username = user.Username,
                mustChangePassword = user.MustChangePassword
            });
        }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } =
            string.Empty;

        public string Password { get; set; } =
            string.Empty;

        public string Role { get; set; } =
            "Student";
    }

    public class ChangeRoleRequest
    {
        public string Role { get; set; } =
            string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } =
            string.Empty;
    }
}