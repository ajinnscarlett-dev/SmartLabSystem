using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class StudentAccountWorkflowTests
{
    [Fact]
    public async Task BulkImportCreatesStudentWithStudentNumberPasswordAndChangeFlag()
    {
        await using AppDbContext context = CreateContext();
        var controller = new UserManagementController(context);

        IActionResult result = await controller.ImportStudents(new UserManagementController.StudentImportBatchRequest
        {
            Rows = new List<UserManagementController.StudentImportRowRequest>
            {
                new() { RowNumber = 2, StudentNumber = "2026-0001", StudentName = "Test Student" }
            }
        });

        Assert.IsType<OkObjectResult>(result);

        User user = await context.Users.SingleAsync();
        Assert.Equal("2026-0001", user.Username);
        Assert.Equal("2026-0001", user.StudentNumber);
        Assert.Equal("Test Student", user.FullName);
        Assert.Equal("Student", user.Role);
        Assert.True(user.MustChangePassword);

        var hasher = new PasswordHasher<User>();
        Assert.NotEqual("2026-0001", user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, user.PasswordHash, "2026-0001"));
    }

    [Fact]
    public async Task BulkImportRejectsMissingFieldsAndDuplicatesWithoutDuplicateAccounts()
    {
        await using AppDbContext context = CreateContext();
        context.Users.Add(new User
        {
            Username = "2026-0001",
            StudentNumber = "2026-0001",
            FullName = "Existing Student",
            PasswordHash = "hash",
            Role = "Student",
            MustChangePassword = true
        });
        await context.SaveChangesAsync();

        var controller = new UserManagementController(context);
        IActionResult result = await controller.ImportStudents(new UserManagementController.StudentImportBatchRequest
        {
            Rows = new List<UserManagementController.StudentImportRowRequest>
            {
                new() { RowNumber = 2, StudentNumber = "", StudentName = "Missing Number" },
                new() { RowNumber = 3, StudentNumber = "2026-0002", StudentName = "" },
                new() { RowNumber = 4, StudentNumber = "2026-0001", StudentName = "Existing Student" },
                new() { RowNumber = 5, StudentNumber = "2026-0003", StudentName = "New Student" },
                new() { RowNumber = 6, StudentNumber = "2026-0003", StudentName = "Duplicate New Student" }
            }
        });

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, await context.Users.CountAsync());

        User imported = await context.Users.SingleAsync(u => u.StudentNumber == "2026-0003");
        Assert.Equal("New Student", imported.FullName);
        Assert.True(imported.MustChangePassword);

        string json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("Missing Student Number.", json);
        Assert.Contains("Missing Student Name.", json);
        Assert.Contains("already uses this Student Number", json);
        Assert.Contains("Duplicate Student Number in this import.", json);
    }

    [Fact]
    public async Task DisabledAccountCannotReceiveJwt()
    {
        await using AppDbContext context = CreateContext();
        User user = new User
        {
            UserId = 50,
            Username = "disabled-user",
            Role = "Disabled:Student",
            MustChangePassword = true,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Password123");
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
            configuration);

        IActionResult result = await controller.Login(new LoginRequest
        {
            Username = "disabled-user",
            Password = "Password123"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
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