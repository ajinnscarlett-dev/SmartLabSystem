using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class OwnershipRecoveryTests
{
    [Fact]
    public async Task StudentCannotClaimTwoPcsAndLogoutRequiresExactSession()
    {
        await using var db = Create();
        await Seed(db);
        var controller = Controller(db, 1);
        Assert.IsType<OkObjectResult>(await controller.LoginToPC("PC01", 1, Login()));
        var session = await db.PcUsageHistory.SingleAsync();
        Assert.IsType<ConflictObjectResult>(await controller.LoginToPC("PC02", 1, Login()));
        Assert.IsType<ConflictObjectResult>(await controller.ReleasePC(1, 2, session.SessionId));
        Assert.IsType<ConflictObjectResult>(await controller.ReleasePC(1, 1, session.SessionId + 1));
        Assert.IsType<ForbidResult>(await Controller(db, 2).ReleasePC(1, 1, session.SessionId));
        Assert.IsType<OkObjectResult>(await controller.ReleasePC(1, 1, session.SessionId));
        Assert.Null((await db.PCs.FindAsync(1))!.CurrentUserId);
        Assert.NotNull(session.LogoutTime);
    }

    [Fact]
    public async Task OutageRetainsSessionAndClientRestartRecoversSameOwnership()
    {
        await using var db = Create();
        await Seed(db);
        var controller = Controller(db, 1);
        await controller.LoginToPC("PC01", 1, Login());
        var session = await db.PcUsageHistory.SingleAsync();
        var pc = (await db.PCs.FindAsync(1))!;
        pc.Status = "Offline";
        await db.SaveChangesAsync();
        Assert.Null(session.LogoutTime);
        Assert.IsType<ConflictObjectResult>(await Controller(db, 2).LoginToPC("PC01", 2, Login()));
        Assert.IsType<OkObjectResult>(await controller.LoginToPC("PC01", 1, Login()));
        Assert.Equal("Occupied", pc.Status);
        Assert.Equal(session.SessionId, (await db.PcUsageHistory.SingleAsync()).SessionId);
        Assert.IsType<ForbidResult>(await Controller(db, 2).Heartbeat(1, session.SessionId));
        Assert.IsType<OkObjectResult>(await controller.Heartbeat(1, session.SessionId));
    }

    [Fact]
    public async Task StaleHeartbeatCannotRestoreReleasedSession()
    {
        await using var db = Create();
        await Seed(db);
        var controller = Controller(db, 1);
        await controller.LoginToPC("PC01", 1, Login());
        long oldSession = (await db.PcUsageHistory.SingleAsync()).SessionId;
        await controller.ReleasePC(1, 1, oldSession);
        await controller.LoginToPC("PC01", 1, Login());
        Assert.IsType<ConflictObjectResult>(await controller.Heartbeat(1, oldSession));
        Assert.IsType<ConflictObjectResult>(await controller.ReleasePC(1, 1, oldSession));
    }

    [Fact]
    public async Task TwoContextsCannotOverwriteSamePcOwner()
    {
        string name = Guid.NewGuid().ToString();
        await using var first = Create(name);
        await Seed(first);
        await using var second = Create(name);
        var pc1 = (await first.PCs.FindAsync(1))!;
        var pc2 = (await second.PCs.FindAsync(1))!;
        pc1.CurrentUserId = 1; pc1.Status = "Occupied";
        pc2.CurrentUserId = 2; pc2.Status = "Occupied";
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public void SqlModelHasUniqueNonNullOwnerIndex()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var index = db.Model.FindEntityType(typeof(PC))!.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == "CurrentUserId");
        Assert.True(index.IsUnique);
        Assert.Equal("[CurrentUserId] IS NOT NULL", index.GetFilter());
    }

    private static PCLoginRequest Login() => new() { MACAddress = "AABBCCDDEEFF" };
    private static AppDbContext Create(string? name = null) => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);
    private static async Task Seed(AppDbContext db)
    {
        db.Users.AddRange(new User { UserId = 1, Username = "one", PasswordHash = "hash", Role = "Student", MustChangePassword = false }, new User { UserId = 2, Username = "two", PasswordHash = "hash", Role = "Student", MustChangePassword = false });
        db.PCs.AddRange(new PC { PCId = 1, PCNumber = "PC01", MACAddress = "AABBCCDDEEFF" }, new PC { PCId = 2, PCNumber = "PC02", MACAddress = "AABBCCDDEEFF" });
        await db.SaveChangesAsync();
    }
    private static PCController Controller(AppDbContext db, int user) => new(db)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, "Student") }, "test")) } }
    };
}
