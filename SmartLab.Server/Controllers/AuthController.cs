using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace SmartLab.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }


        // =========================================================
        // LOGIN
        // =========================================================

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required."
                });
            }


            // FIND USER
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);


            // USER NOT FOUND
            if (user == null)
            {
                // Log failed login attempt
                var failedLog = new ActivityLog
                {
                    UserId = null,
                    PCId = null,
                    Action = "Login Failed",
                    Details = $"Failed login attempt for username: {request.Username}",
                    CreatedAt = DateTime.Now
                };

                _context.ActivityLogs.Add(failedLog);
                await _context.SaveChangesAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }


            // VERIFY PASSWORD
            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
            );


            // WRONG PASSWORD
            if (result == PasswordVerificationResult.Failed)
            {
                // Log failed login attempt
                var failedLog = new ActivityLog
                {
                    UserId = user.UserId,
                    PCId = null,
                    Action = "Login Failed",
                    Details = $"Failed login attempt for user: {user.Username}",
                    CreatedAt = DateTime.Now
                };

                _context.ActivityLogs.Add(failedLog);
                await _context.SaveChangesAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }


            // =====================================================
            // SUCCESSFUL LOGIN
            // =====================================================

            var loginLog = new ActivityLog
            {
                UserId = user.UserId,
                PCId = null,
                Action = "Login",
                Details = $"User {user.Username} logged in successfully.",
                CreatedAt = DateTime.Now
            };

            _context.ActivityLogs.Add(loginLog);

            await _context.SaveChangesAsync();


            return Ok(new
            {
                message = "Login successful!",
                userId = user.UserId,
                username = user.Username,
                role = user.Role
            });
        }


        // =========================================================
        // REGISTER
        // =========================================================

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required."
                });
            }


            // CHECK EXISTING USER
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);


            if (existingUser != null)
            {
                return Conflict(new
                {
                    message = "Username already exists."
                });
            }


            // CREATE USER
            var user = new User
            {
                Username = request.Username,

                Role = string.IsNullOrWhiteSpace(request.Role)
                    ? "Student"
                    : request.Role,

                CreatedAt = DateTime.Now
            };


            // HASH PASSWORD
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password
            );


            // SAVE USER
            _context.Users.Add(user);

            await _context.SaveChangesAsync();


            // =====================================================
            // ACTIVITY LOG - REGISTRATION
            // =====================================================

            var registrationLog = new ActivityLog
            {
                UserId = user.UserId,
                PCId = null,
                Action = "Registration",
                Details = $"New {user.Role} account created: {user.Username}",
                CreatedAt = DateTime.Now
            };

            _context.ActivityLogs.Add(registrationLog);

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


    // =============================================================
    // LOGIN REQUEST
    // =============================================================

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }


    // =============================================================
    // REGISTER REQUEST
    // =============================================================

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Student";
    }
}