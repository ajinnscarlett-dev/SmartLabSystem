using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const string DisabledPrefix = "__SMARTLAB_DISABLED__|";
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly AuthTokenService _tokenService;
        private readonly IHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public AuthController(
            AppDbContext context,
            AuthTokenService tokenService,
            IHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _tokenService = tokenService;
            _environment = environment;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            string normalizedUsername = request.Username?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            User? user = null;
            if (_environment.IsDevelopment() &&
                normalizedUsername.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                user = await EnsureDevelopmentAdminAsync(request.Password);
            }

            user ??= await _context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername);

            // Deactivated accounts keep their original username encoded in a
            // disabled wrapper. Resolve that wrapper so inactive users receive
            // the correct account-status response instead of a misleading
            // invalid-credentials response. Active accounts always win if the
            // same username has since been reused.
            if (user == null)
            {
                List<User> disabledUsers = await _context.Users
                    .Where(u => u.Username.StartsWith(DisabledPrefix))
                    .ToListAsync();

                user = disabledUsers.FirstOrDefault(u =>
                    string.Equals(
                        TryGetDisabledDisplayUsername(u.Username),
                        normalizedUsername,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (user == null)
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = null,
                    PCId = null,
                    Action = "Login Failed",
                    Details = $"Failed login attempt for username: {normalizedUsername}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Unauthorized(new { message = "Invalid username or password." });
            }

            if (user.Role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase))
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Login Failed",
                    Details = $"Login blocked for inactive user account: {normalizedUsername}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Unauthorized(new { message = "This account is inactive." });
            }

            PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Login Failed",
                    Details = $"Failed login attempt for user: {user.Username}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Unauthorized(new { message = "Invalid username or password." });
            }

            string token = _tokenService.CreateToken(user);

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = user.UserId,
                PCId = null,
                Action = "Login",
                Details = $"User {user.Username} logged in successfully.",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Login successful!",
                token,
                expiresInHours = 8,
                userId = user.UserId,
                username = user.Username,
                role = user.Role,
                mustChangePassword = user.MustChangePassword
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out int userId))
            {
                return Unauthorized(new { message = "Authenticated user ID is missing." });
            }

            string currentPassword = request.CurrentPassword ?? string.Empty;
            string newPassword = request.NewPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                return BadRequest(new { message = "Current password and new password are required." });
            }

            if (newPassword.Length < 6)
            {
                return BadRequest(new { message = "New password must be at least 6 characters." });
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "New password must be different from the current password." });
            }

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return Unauthorized(new { message = "User account no longer exists." });
            }

            if (user.Role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "This account is inactive." });
            }

            PasswordVerificationResult verification = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                currentPassword);

            if (verification == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.MustChangePassword = false;

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = user.UserId,
                PCId = null,
                Action = "Password Changed",
                Details = $"User {user.Username} changed their password successfully.",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully." });
        }

        private static string? TryGetDisabledDisplayUsername(string username)
        {
            const string disabledPrefix = DisabledPrefix;

            if (!username.StartsWith(disabledPrefix, StringComparison.Ordinal))
                return null;

            string payload = username[disabledPrefix.Length..];
            int separator = payload.IndexOf('|');
            if (separator <= 0)
                return null;

            try
            {
                return System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(payload[..separator]));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private async Task<User?> EnsureDevelopmentAdminAsync(string suppliedPassword)
        {
            bool bootstrapEnabled = _configuration.GetValue("SmartLab:EnableDevelopmentBootstrap", false);
            if (!bootstrapEnabled)
                return null;

            string bootstrapUsername =
                _configuration["SmartLab:DevelopmentAdminUsername"]?.Trim()
                ?? Environment.GetEnvironmentVariable("SMARTLAB_DEV_ADMIN_USERNAME")?.Trim()
                ?? string.Empty;

            string bootstrapPassword =
                _configuration["SmartLab:DevelopmentAdminPassword"]
                ?? Environment.GetEnvironmentVariable("SMARTLAB_DEV_ADMIN_PASSWORD")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(bootstrapUsername) ||
                string.IsNullOrWhiteSpace(bootstrapPassword) ||
                !bootstrapUsername.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(suppliedPassword, bootstrapPassword, StringComparison.Ordinal))
            {
                return null;
            }

            User? admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == bootstrapUsername);

            if (admin == null)
            {
                admin = new User
                {
                    Username = bootstrapUsername,
                    Role = "Admin",
                    MustChangePassword = false,
                    CreatedAt = DateTime.Now
                };

                admin.PasswordHash = _passwordHasher.HashPassword(admin, bootstrapPassword);
                _context.Users.Add(admin);
                await _context.SaveChangesAsync();
                return admin;
            }

            bool passwordMatches = _passwordHasher.VerifyHashedPassword(
                admin,
                admin.PasswordHash,
                bootstrapPassword) != PasswordVerificationResult.Failed;

            bool roleMatches = string.Equals(admin.Role, "Admin", StringComparison.OrdinalIgnoreCase);

            if (!passwordMatches || !roleMatches || admin.MustChangePassword)
            {
                admin.Role = "Admin";
                admin.PasswordHash = _passwordHasher.HashPassword(admin, bootstrapPassword);
                admin.MustChangePassword = false;
                await _context.SaveChangesAsync();
            }

            return admin;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            string normalizedUsername = request.Username?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            User? existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername);
            if (existingUser != null)
                return Conflict(new { message = "Username already exists." });

            var user = new User
            {
                Username = normalizedUsername,
                Role = "Student",
                MustChangePassword = true,
                CreatedAt = DateTime.Now
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = user.UserId,
                PCId = null,
                Action = "Registration",
                Details = $"New Student account created: {user.Username}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration successful!",
                userId = user.UserId,
                username = user.Username,
                role = user.Role,
                mustChangePassword = user.MustChangePassword
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Kept for compatibility with the existing client.
        // The server intentionally ignores this value and always creates public registrations as Student.
        public string Role { get; set; } = "Student";
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}