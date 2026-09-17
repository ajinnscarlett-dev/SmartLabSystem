using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using System.Security.Claims;
using Xunit;

namespace SmartLab.Tests;

public sealed class PasswordPolicyTests
{
    [Fact]
    public async Task SuccessfulPasswordChangeClearsMustChangePassword()
    {
        await using AppDbContext context = CreateContext();
        User user = new()
        {
            UserId = 70,
            Username = "student70",
            Role = "Student",
            MustChangePassword = true,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "OldPass123");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SmartLab-Tests-Only-Key-2026-At-Least-32-Chars!",
                ["Jwt:Issuer"] = "SmartLab",
                ["Jwt:Audience"] = "SmartLab.Client"
            })
            .Build();

        var controller = new AuthController(
            context,
            new AuthTokenService(configuration),
            new TestHostEnvironment(),
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "70"),
                        new Claim(ClaimTypes.Role, "Student")
                    }, "Test"))
                }
            }
        };

        IActionResult result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123",
            NewPassword = "NewPass456"
        });

        Assert.IsType<OkObjectResult>(result);
        User updated = await context.Users.SingleAsync();
        Assert.False(updated.MustChangePassword);
        Assert.NotEqual("NewPass456", updated.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(updated, updated.PasswordHash, "NewPass456"));
    }

    [Fact]
    public async Task AdminPasswordResetForcesNextLoginChange()
    {
        await using AppDbContext context = CreateContext();
        User user = new()
        {
            UserId = 80,
            Username = "student80",
            Role = "Student",
            MustChangePassword = false,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "OldPass123");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UserManagementController(context);
        IActionResult result = await controller.ResetPassword(80, new UserManagementController.PasswordResetRequest
        {
            NewPassword = "ResetPass456"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.True((await context.Users.SingleAsync()).MustChangePassword);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "SmartLab.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}