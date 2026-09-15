using Microsoft.EntityFrameworkCore;
using SmartLab.Server;

namespace SmartLab.Tests;

public class AppDbContextOperationalTests
{
    private static AppDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task OccupyingPcCreatesPersistentUsageSession()
    {
        await using var context = CreateContext(nameof(OccupyingPcCreatesPersistentUsageSession));

        var user = new User
        {
            UserId = 1,
            Username = "student01",
            PasswordHash = "hash",
            Role = "Student"
        };

        var lab = new Laboratory
        {
            LaboratoryId = 10,
            LabName = "COMLAB 601"
        };

        var pc = new PC
        {
            PCId = 20,
            PCNumber = "601-01",
            Status = "Available",
            IsEnabled = true,
            LaboratoryId = lab.LaboratoryId
        };

        context.Users.Add(user);
        context.Laboratories.Add(lab);
        context.PCs.Add(pc);
        await context.SaveChangesAsync();

        pc.Status = "Occupied";
        pc.CurrentUserId = user.UserId;
        await context.SaveChangesAsync();

        var session = await context.PcUsageHistory.SingleAsync();

        Assert.Equal(pc.PCId, session.PCId);
        Assert.Equal(user.UserId, session.UserId);
        Assert.Null(session.LogoutTime);
        Assert.Equal(string.Empty, session.EndReason);
    }

    [Fact]
    public async Task OfflineTransitionClosesUsageSessionWithHeartbeatTimeout()
    {
        await using var context = CreateContext(nameof(OfflineTransitionClosesUsageSessionWithHeartbeatTimeout));

        var user = new User
        {
            UserId = 1,
            Username = "student01",
            PasswordHash = "hash",
            Role = "Student"
        };

        var pc = new PC
        {
            PCId = 20,
            PCNumber = "601-01",
            Status = "Available",
            IsEnabled = true
        };

        context.Users.Add(user);
        context.PCs.Add(pc);
        await context.SaveChangesAsync();

        pc.Status = "Occupied";
        pc.CurrentUserId = user.UserId;
        await context.SaveChangesAsync();

        pc.Status = "Offline";
        pc.CurrentUserId = null;
        await context.SaveChangesAsync();

        var session = await context.PcUsageHistory.SingleAsync();

        Assert.NotNull(session.LogoutTime);
        Assert.Equal("HeartbeatTimeout", session.EndReason);
        Assert.True(session.DurationSeconds >= 0);
    }

    [Fact]
    public async Task MaintenanceTransitionCreatesAndClosesMaintenanceRecord()
    {
        await using var context = CreateContext(nameof(MaintenanceTransitionCreatesAndClosesMaintenanceRecord));

        var pc = new PC
        {
            PCId = 20,
            PCNumber = "601-01",
            Status = "Available",
            IsEnabled = true
        };

        context.PCs.Add(pc);
        await context.SaveChangesAsync();

        pc.Status = "Maintenance";
        pc.MaintenanceReason = "Keyboard replacement";
        pc.MaintenanceStarted = DateTime.Now;
        await context.SaveChangesAsync();

        var record = await context.MaintenanceRecords.SingleAsync();
        Assert.Equal("Keyboard replacement", record.Reason);
        Assert.Null(record.EndedAt);

        pc.Status = "Available";
        await context.SaveChangesAsync();

        record = await context.MaintenanceRecords.SingleAsync();
        Assert.NotNull(record.EndedAt);
    }

    [Fact]
    public async Task NewAnnouncementCreatesNotificationsForTargetRole()
    {
        await using var context = CreateContext(nameof(NewAnnouncementCreatesNotificationsForTargetRole));

        context.Users.AddRange(
            new User
            {
                UserId = 1,
                Username = "teacher01",
                PasswordHash = "hash",
                Role = "Teacher"
            },
            new User
            {
                UserId = 2,
                Username = "student01",
                PasswordHash = "hash",
                Role = "Student"
            });

        await context.SaveChangesAsync();

        context.Announcements.Add(new Announcement
        {
            AnnouncementId = 100,
            Title = "Teacher Notice",
            Message = "Please check your assigned laboratory.",
            IsActive = true,
            TargetRole = "Teacher",
            CreatedAt = DateTime.Now
        });

        await context.SaveChangesAsync();

        var notification = await context.Notifications.SingleAsync();
        Assert.Equal(1, notification.UserId);
        Assert.Equal("Announcement", notification.Type);
        Assert.False(notification.IsRead);
    }
}
