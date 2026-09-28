using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class CommandAuthorizationTests
{
    [Fact]
    public async Task CommandStatusUsesTargetLaboratoryAndCurrentSchedule()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        var admin = WithUser(new PCCommandController(db), 10, "Admin");
        var queued = Assert.IsType<OkObjectResult>(await admin.LockComputer(pcId));
        long commandId = JsonSerializer.SerializeToElement(queued.Value).GetProperty("commandId").GetInt64();
        Assert.IsType<ForbidResult>(await WithUser(new PCCommandController(db), 20, "Teacher").GetCommandStatus(commandId));
        Assert.IsType<ForbidResult>(await WithUser(new PCCommandController(db), 1, "Student").GetCommandStatus(commandId));
        AddSchedule(db, 20);
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await WithUser(new PCCommandController(db), 20, "Teacher").GetCommandStatus(commandId));
        db.ClassSchedules.Single().Status = "Cancelled";
        await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await WithUser(new PCCommandController(db), 20, "Teacher").GetCommandStatus(commandId));
        Assert.IsType<OkObjectResult>(await admin.GetCommandStatus(commandId));
    }

    [Fact]
    public async Task StudentsCannotQueueCommandsAndRemoteInputNeedsActiveSession()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        Assert.IsType<ForbidResult>(await WithUser(new PCCommandController(db), 1, "Student").LockComputer(pcId));
        Assert.IsType<ConflictObjectResult>(await WithUser(new PCCommandController(db), 10, "Admin")
            .RemoteKeyPress(pcId, new() { KeyCode = 65 }));
        Assert.IsType<ObjectResult>(await WithUser(new PCCommandController(db), 20, "Teacher").LockComputer(pcId));
    }

    [Fact]
    public async Task QueuedCommandCannotCrossStudentSessions()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        var queued = Assert.IsType<OkObjectResult>(await WithUser(new PCCommandController(db), 10, "Admin").LockComputer(pcId));
        long commandId = JsonSerializer.SerializeToElement(queued.Value).GetProperty("commandId").GetInt64();
        var pc = (await db.PCs.FindAsync(pcId))!;
        pc.CurrentUserId = null; pc.Status = "Available"; await db.SaveChangesAsync();
        pc.CurrentUserId = 1; pc.Status = "Occupied"; await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await WithUser(new PCCommandController(db), 1, "Student")
            .CompleteCommand(commandId, new() { Success = true }));
    }

    [Fact]
    public async Task MonitoringEnforcesRoleAndLiveLaboratorySchedule()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        ScreenMonitorController Monitor(int user, string role) => WithUser(new ScreenMonitorController(db, new(db)), user, role);
        Assert.IsType<ForbidResult>(await Monitor(1, "Student").StartMonitoring(pcId));
        Assert.IsType<ForbidResult>(await Monitor(20, "Teacher").StartMonitoring(pcId));
        Assert.IsType<OkObjectResult>(await Monitor(10, "Admin").StartMonitoring(pcId));
        Assert.IsType<ForbidResult>(await Monitor(20, "Teacher").GetFrameMetadata(pcId));
        AddSchedule(db, 20); await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Monitor(20, "Teacher").StartMonitoring(pcId));
        Assert.IsType<ForbidResult>(await Monitor(2, "Student").GetMonitoringStatus(pcId));
        Assert.IsType<OkObjectResult>(await Monitor(1, "Student").GetMonitoringStatus(pcId));
    }

    [Fact]
    public async Task ChunkedScreenUploadIsBoundedAndOldSessionFrameIsRejected()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        var monitor = WithUser(new ScreenMonitorController(db, new(db)), 10, "Admin");
        await monitor.StartMonitoring(pcId);
        var student = WithUser(new ScreenMonitorController(db, new(db)), 1, "Student");
        student.Request.Body = new MemoryStream(new byte[4 * 1024 * 1024 + 1]);
        Assert.Null(student.Request.ContentLength);
        Assert.IsType<BadRequestObjectResult>(await student.UploadScreen(pcId));
        student.Request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.IsType<OkObjectResult>(await student.UploadScreen(pcId));
        Assert.IsType<OkObjectResult>(await monitor.GetFrameMetadata(pcId));
        var pc = (await db.PCs.FindAsync(pcId))!;
        pc.CurrentUserId = null; pc.Status = "Available"; await db.SaveChangesAsync();
        Assert.IsType<NotFoundObjectResult>(await monitor.GetFrameMetadata(pcId));
    }

    [Fact]
    public async Task HardwareReadRequiresCurrentScheduleEvenWithCachedAuthorization()
    {
        await using var db = Create();
        int pcId = Random.Shared.Next(10000, int.MaxValue);
        await Seed(db, pcId);
        db.HardwareInventories.Add(new HardwareInventory { PCId = pcId });
        db.TeacherLaboratoryAuthorizations.Add(new TeacherLaboratoryAuthorization { TeacherUserId = 20, LaboratoryId = 601 });
        await db.SaveChangesAsync();
        var teacher = WithUser(new HardwareInventoryController(db), 20, "Teacher");
        Assert.IsType<ForbidResult>(await teacher.GetOne(pcId));
        AddSchedule(db, 20); await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await teacher.GetOne(pcId));
    }

    private static AppDbContext Create() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task Seed(AppDbContext db, int pcId)
    {
        db.Users.Add(new User { UserId = 1, Username = "student", PasswordHash = "hash", Role = "Student" });
        db.PCs.Add(new PC { PCId = pcId, PCNumber = $"PC{pcId}", LaboratoryId = 601 });
        await db.SaveChangesAsync();
        var pc = (await db.PCs.FindAsync(pcId))!; pc.Status = "Occupied"; pc.CurrentUserId = 1;
        await db.SaveChangesAsync();
    }
    private static void AddSchedule(AppDbContext db, int user) => db.ClassSchedules.Add(new ClassSchedule
    {
        TeacherUserId = user, LaboratoryId = 601, ScheduleDate = DateTime.Today,
        StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromDays(1), Status = "Scheduled"
    });
    private static T WithUser<T>(T controller, int id, string role) where T : ControllerBase
    {
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) }, "test")) } };
        return controller;
    }
}
