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

        // =========================================================
        // LOGIN
        // =========================================================

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            string normalizedUsername =
                request.Username?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required."
                });
            }

            // Development-only deterministic Admin bootstrap. This is
            // enabled only by the Development launch profile and never
            // grants Admin access in production.
            User? user = null;
            if (_environment.IsDevelopment() &&
                normalizedUsername.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                user = await EnsureDevelopmentAdminAsync(request.Password);
            }

            user ??= await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Username == normalizedUsername);

            if (user == null)
            {
                _context.ActivityLogs.Add(
                    new ActivityLog
                    {
                        UserId = null,
                        PCId = null,
                        Action = "Login Failed",
                        Details =
                            $"Failed login attempt for username: {normalizedUsername}",
                        CreatedAt = DateTime.Now
                    });

                await _context.SaveChangesAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }

            PasswordVerificationResult result =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    request.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                _context.ActivityLogs.Add(
                    new ActivityLog
                    {
                        UserId = user.UserId,
                        PCId = null,
                        Action = "Login Failed",
                        Details =
                            $"Failed login attempt for user: {user.Username}",
                        CreatedAt = DateTime.Now
                    });

                await _context.SaveChangesAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }

            string token =
                _tokenService.CreateToken(user);

            _context.ActivityLogs.Add(
                new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Login",
                    Details =
                        $"User {user.Username} logged in successfully.",
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
                role = user.Role
            });
        }

        private async Task<User?> EnsureDevelopmentAdminAsync(string suppliedPassword)
        {
            bool bootstrapEnabled = _configuration.GetValue(
                "SmartLab:EnableDevelopmentBootstrap",
                false);

            if (!bootstrapEnabled)
            {
                return null;
            }

            string bootstrapUsername =
                _configuration["SmartLab:DevelopmentAdminUsername"]?.Trim()
                ?? string.Empty;
            string bootstrapPassword =
                _configuration["SmartLab:DevelopmentAdminPassword"]
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(bootstrapUsername) ||
                string.IsNullOrWhiteSpace(bootstrapPassword) ||
                !bootstrapUsername.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(suppliedPassword, bootstrapPassword, StringComparison.Ordinal))
            {
                return null;
            }

            User? admin = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == bootstrapUsername);

            if (admin == null)
            {
                admin = new User
                {
                    Username = bootstrapUsername,
                    Role = "Admin",
                    CreatedAt = DateTime.Now
                };

                admin.PasswordHash = _passwordHasher.HashPassword(
                    admin,
                    bootstrapPassword);

                _context.Users.Add(admin);
                await _context.SaveChangesAsync();
                return admin;
            }

            bool passwordMatches =
                _passwordHasher.VerifyHashedPassword(
                    admin,
                    admin.PasswordHash,
                    bootstrapPassword) != PasswordVerificationResult.Failed;

            bool roleMatches =
                admin.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            if (!passwordMatches || !roleMatches)
            {
                admin.Role = "Admin";
                admin.PasswordHash = _passwordHasher.HashPassword(
                    admin,
                    bootstrapPassword);

                await _context.SaveChangesAsync();
            }

            return admin;
        }

        // =========================================================
        // REGISTER
        // =========================================================

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            string normalizedUsername =
                request.Username?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required."
                });
            }

            var existingUser =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.Username == normalizedUsername);

            if (existingUser != null)
            {
                return Conflict(new
                {
                    message = "Username already exists."
                });
            }

            var user = new User
            {
                Username = normalizedUsername,
                Role = "Student",
                CreatedAt = DateTime.Now
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.ActivityLogs.Add(
                new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Registration",
                    Details =
                        $"New Student account created: {user.Username}",
                    CreatedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration successful!",
                userId = user.UserId,
                username = user.Username,
                role = user.Role
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
        // Server intentionally ignores this value and always
        // creates public registrations as Student.
        public string Role { get; set; } = "Student";
    }
}
