using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class TeacherAuthenticationTests
{
    [Fact]
    public async Task TeacherAccountCreationProducesTeacherRoleAndHashedPassword()
    {
        await using AppDbContext context = CreateContext();
        var controller = new UserManagementController(context);

        IActionResult result = await controller.Create(new UserManagementController.CreateAccountRequest
        {
            Username = "teacher01",
            Password = "TeacherPass123",
            Role = "teacher"
        });

        Assert.IsType<OkObjectResult>(result);

        User user = await context.Users.SingleAsync();
        Assert.Equal("teacher01", user.Username);
        Assert.Equal("Teacher", user.Role);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual("TeacherPass123", user.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, "TeacherPass123"));
    }

    [Fact]
    public async Task TeacherLoginReturnsTeacherJwtAndPasswordPolicyFlag()
    {
        await using AppDbContext context = CreateContext();
        User teacher = CreateTeacher(10, "teacher01", "TeacherPass123", mustChangePassword: true);
        context.Users.Add(teacher);
        await context.SaveChangesAsync();

        IConfiguration configuration = CreateJwtConfiguration();
        var controller = CreateAuthController(context, configuration);

        IActionResult result = await controller.Login(new LoginRequest
        {
            Username = "teacher01",
            Password = "TeacherPass123"
        });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        using JsonDocument document = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(ok.Value));
        JsonElement root = document.RootElement;

        Assert.Equal("Teacher", root.GetProperty("role").GetString());
        Assert.Equal(10, root.GetProperty("userId").GetInt32());
        Assert.Equal("teacher01", root.GetProperty("username").GetString());
        Assert.True(root.GetProperty("mustChangePassword").GetBoolean());

        string token = root.GetProperty("token").GetString()!;
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("SmartLab", jwt.Issuer);
        Assert.Contains("SmartLab.Client", jwt.Audiences);
        Assert.Equal("10", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("teacher01", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Teacher", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task DisabledTeacherCannotLogin()
    {
        await using AppDbContext context = CreateContext();
        User teacher = CreateTeacher(11, "teacher-disabled", "TeacherPass123", mustChangePassword: false);
        teacher.Role = "Disabled:Teacher";
        context.Users.Add(teacher);
        await context.SaveChangesAsync();

        IConfiguration configuration = CreateJwtConfiguration();
        var controller = CreateAuthController(context, configuration);

        IActionResult result = await controller.Login(new LoginRequest
        {
            Username = "teacher-disabled",
            Password = "TeacherPass123"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task TeacherPasswordResetPreservesRoleAndForcesChange()
    {
        await using AppDbContext context = CreateContext();
        User teacher = CreateTeacher(12, "teacher-reset", "OldPass123", mustChangePassword: false);
        context.Users.Add(teacher);
        await context.SaveChangesAsync();

        var controller = new UserManagementController(context);

        IActionResult result = await controller.ResetPassword(
            12,
            new UserManagementController.PasswordResetRequest
            {
                NewPassword = "ResetPass456"
            });

        Assert.IsType<OkObjectResult>(result);

        User updated = await context.Users.SingleAsync();
        Assert.Equal("Teacher", updated.Role);
        Assert.True(updated.MustChangePassword);
        Assert.NotEqual("ResetPass456", updated.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(updated, updated.PasswordHash, "ResetPass456"));
    }

    [Fact]
    public async Task TeacherPasswordChangeClearsChangeRequirement()
    {
        await using AppDbContext context = CreateContext();
        User teacher = CreateTeacher(13, "teacher-change", "OldPass123", mustChangePassword: true);
        context.Users.Add(teacher);
        await context.SaveChangesAsync();

        IConfiguration configuration = CreateJwtConfiguration();
        var controller = CreateAuthController(context, configuration, 13, "Teacher");

        IActionResult result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123",
            NewPassword = "NewPass456"
        });

        Assert.IsType<OkObjectResult>(result);
        User updated = await context.Users.SingleAsync();
        Assert.Equal("Teacher", updated.Role);
        Assert.False(updated.MustChangePassword);
        Assert.NotEqual("NewPass456", updated.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(updated, updated.PasswordHash, "NewPass456"));
    }

    [Fact]
    public async Task TeacherCanReadCurrentScheduleForOwnUser()
    {
        await using AppDbContext context = CreateContext();
        DateTime now = DateTime.Now;
        User teacher = CreateTeacher(14, "teacher-schedule", "TeacherPass123", mustChangePassword: false);
        Laboratory lab = new() { LaboratoryId = 601, LabName = "COMLAB 601" };

        context.Users.Add(teacher);
        context.Laboratories.Add(lab);
        context.ClassSchedules.Add(new ClassSchedule
        {
            TeacherUserId = teacher.UserId,
            TeacherUser = teacher,
            LaboratoryId = lab.LaboratoryId,
            Laboratory = lab,
            SubjectName = "Programming 2",
            ClassName = "BSIT-2A",
            ScheduleDate = now.Date,
            StartTime = now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5)),
            EndTime = now.TimeOfDay.Add(TimeSpan.FromMinutes(5)),
            Status = "Scheduled",
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var controller = new ScheduleController(context, new TeacherScheduleService(context))
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(teacher.UserId, "Teacher") }
        };

        IActionResult result = await controller.GetCurrent();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Programming 2", json);
        Assert.Contains("COMLAB 601", json);
    }

    [Fact]
    public async Task TeacherOutsideScheduleCannotReadLaboratory()
    {
        await using AppDbContext context = CreateContext();
        User teacher = CreateTeacher(15, "teacher-outside", "TeacherPass123", mustChangePassword: false);
        Laboratory lab = new() { LaboratoryId = 603, LabName = "COMLAB 603" };

        context.Users.Add(teacher);
        context.Laboratories.Add(lab);
        context.ClassSchedules.Add(new ClassSchedule
        {
            TeacherUserId = teacher.UserId,
            TeacherUser = teacher,
            LaboratoryId = lab.LaboratoryId,
            Laboratory = lab,
            SubjectName = "Programming 2",
            ClassName = "BSIT-2A",
            ScheduleDate = DateTime.Today.AddDays(1),
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(10),
            Status = "Scheduled",
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var controller = new ScheduleController(context, new TeacherScheduleService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(teacher.UserId, "Teacher")
            }
        };

        IActionResult result = await controller.GetLaboratory(603, DateTime.Today);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task TeacherCannotUseAdminOnlyPcOperations()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "16"),
                    new Claim(ClaimTypes.Role, "Teacher")
                },
                "unit-test"))
        };

        var actionContext = new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor
            {
                RouteValues = new Dictionary<string, string?>
                {
                    ["controller"] = "PC",
                    ["action"] = "AddPC"
                }
            });

        var filter = new SmartLabAuthorizationFilter();
        var filterContext = new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(
            actionContext,
            new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());

        await filter.OnAuthorizationAsync(filterContext);

        Assert.IsType<ForbidResult>(filterContext.Result);
    }

    private static User CreateTeacher(
        int userId,
        string username,
        string password,
        bool mustChangePassword)
    {
        User teacher = new()
        {
            UserId = userId,
            Username = username,
            Role = "Teacher",
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTime.Now
        };

        teacher.PasswordHash = new PasswordHasher<User>().HashPassword(teacher, password);
        return teacher;
    }

    private static AuthController CreateAuthController(
        AppDbContext context,
        IConfiguration configuration,
        int? userId = null,
        string? role = null)
    {
        var controller = new AuthController(
            context,
            new AuthTokenService(configuration),
            new TestHostEnvironment(),
            configuration);

        if (userId.HasValue)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(userId.Value, role ?? "Teacher")
            };

        return controller;
    }

    private static DefaultHttpContext CreateHttpContext(int userId, string role)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, $"user{userId}"),
                    new Claim(ClaimTypes.Role, role)
                },
                "unit-test"))
        };
    }

    private static IConfiguration CreateJwtConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SmartLab-Tests-Only-Key-2026-At-Least-32-Chars!",
                ["Jwt:Issuer"] = "SmartLab",
                ["Jwt:Audience"] = "SmartLab.Client"
            })
            .Build();

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "SmartLab.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
