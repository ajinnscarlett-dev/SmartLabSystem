using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class MachinePresenceControllerTests
{
    [Theory]
    [InlineData(null, "Available")]
    [InlineData(7, "Occupied")]
    public async Task RepeatedPresenceRecoversOfflinePcWithoutChangingOwnership(int? owner, string expected)
    {
        await using var db = CreateContext();
        db.Users.Add(new User { UserId = 7, Username = "student", Role = "Student" });
        var pc = new PC { PCId = 1, PCNumber = "601-PC01", MACAddress = "AABBCCDDEEFF", Status = "Offline", CurrentUserId = owner, IsEnabled = true };
        db.PCs.Add(pc);
        await db.SaveChangesAsync();
        var controller = new MachinePresenceController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var request = new MachinePresenceRequest { PCNumber = pc.PCNumber, MACAddress = "AA-BB-CC-DD-EE-FF", IPAddress = "192.168.1.5" };
        Assert.IsType<OkObjectResult>(await controller.AnnouncePresence(request, default));
        request.IPAddress = "192.168.1.6";
        Assert.IsType<OkObjectResult>(await controller.AnnouncePresence(request, default));
        Assert.Equal(expected, pc.Status);
        Assert.Equal(owner, pc.CurrentUserId);
        Assert.Equal("192.168.1.6", pc.IPAddress);
        Assert.NotNull(pc.LastSeen);
    }

    [Fact]
    public async Task PresenceCannotClearMaintenanceOrAcceptAnotherMac()
    {
        await using var db = CreateContext();
        var pc = new PC { PCId = 1, PCNumber = "601-PC01", MACAddress = "AABBCCDDEEFF", Status = "Maintenance", IsEnabled = false };
        db.PCs.Add(pc);
        await db.SaveChangesAsync();
        var controller = new MachinePresenceController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var request = new MachinePresenceRequest { PCNumber = pc.PCNumber, MACAddress = "AABBCCDDEEFF" };
        Assert.IsType<OkObjectResult>(await controller.AnnouncePresence(request, default));
        Assert.Equal("Maintenance", pc.Status);
        Assert.False(pc.IsEnabled);
        DateTime? lastSeen = pc.LastSeen;
        request.MACAddress = "112233445566";
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.AnnouncePresence(request, default)).StatusCode);
        Assert.Equal(lastSeen, pc.LastSeen);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
