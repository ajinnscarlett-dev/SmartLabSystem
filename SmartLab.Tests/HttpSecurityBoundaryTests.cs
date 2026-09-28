using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartLab.Server;
using Xunit;

namespace SmartLab.Tests;

public sealed class HttpSecurityBoundaryTests
{
    private const string Key = "SmartLab-Test-Only-JWT-Signing-Key-Not-For-Deployment";
    private const string Device = "SmartLab-Test-Only-Device-Credential-Not-For-Deployment";

    [Fact]
    public async Task RealPipelineRejectsInvalidExpiredDisabledAndRestrictedUsers()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/PC")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/PC")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(1, "Student", DateTime.UtcNow.AddMinutes(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/PC")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(1, "Student"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/PCCommand/1/shutdown", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(3, "Admin"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/PC")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(4, "Admin"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/PC")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(2, "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/PC")).StatusCode);
    }

    [Fact]
    public async Task PresenceAndStudentLoginRequireProvisionedDevice()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        var presence = new { pcNumber = "PC01", macAddress = "AABBCCDDEEFF" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/PC/presence", presence)).StatusCode);
        client.DefaultRequestHeaders.Add("X-SmartLab-Device-Token", Device);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/PC/presence", presence)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-SmartLab-Device-Token");
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(1, "Student"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/PC/login/PC01/1", presence)).StatusCode);
        client.DefaultRequestHeaders.Add("X-SmartLab-Device-Token", Device);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/PC/login/PC01/1", presence)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/PC/login/PC01/2", presence)).StatusCode);
    }

    [Fact]
    public async Task TeacherPcReadsAndMonitoringRespectLiveSchedule()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(5, "Teacher"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/PC/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/ScreenMonitor/1/start", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ClassSchedules.Add(new ClassSchedule { TeacherUserId = 5, LaboratoryId = 601, ScheduleDate = DateTime.Today, StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromDays(1), Status = "Scheduled" });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/PC/1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/ScreenMonitor/1/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/PC/laboratory/602")).StatusCode);
    }

    [Fact]
    public async Task PasswordChangeUnlocksProtectedEndpointsAndInvalidPasswordFails()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/Auth/login", new { username = "admin", password = "wrong" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(4, "Admin"));
        var result = await client.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = "OldPassword123!", newPassword = "NewPassword123!", confirmPassword = "NewPassword123!" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/PC")).StatusCode);
    }

    [Theory]
    [InlineData("/api/PC")]
    [InlineData("/api/Maintenance")]
    [InlineData("/api/HardwareInventory")]
    [InlineData("/api/User")]
    [InlineData("/api/UsageHistory")]
    public async Task StudentCannotReadAdministrativeResources(string path)
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(1, "Student"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    private static string Token(int id, string role, DateTime? expires = null) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken("SmartLab", "SmartLab.Client", new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) },
            expires: expires ?? DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256)));

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", Key);
            builder.UseSetting("SmartLab:RequireDeviceCredentials", "true");
            builder.UseSetting("SmartLab:DeviceTokenHashes:PC01", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Device))));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database));
                services.AddSingleton<IStartupFilter, SeedFilter>();
            });
        }
    }

    private sealed class SeedFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = new PasswordHasher<User>();
            foreach (var (id, name, role, change) in new[] { (1, "student", "Student", false), (2, "admin", "Admin", false), (3, "disabled", "Disabled:Admin", false), (4, "change", "Admin", true), (5, "teacher", "Teacher", false) })
            {
                var user = new User { UserId = id, Username = name, Role = role, MustChangePassword = change };
                user.PasswordHash = hasher.HashPassword(user, "OldPassword123!");
                db.Users.Add(user);
            }
            db.PCs.Add(new PC { PCId = 1, PCNumber = "PC01", MACAddress = "AABBCCDDEEFF", LaboratoryId = 601 });
            db.SaveChanges();
            next(app);
        };
    }
}
