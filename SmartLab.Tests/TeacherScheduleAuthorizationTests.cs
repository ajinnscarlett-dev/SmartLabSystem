using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using Xunit;

namespace SmartLab.Tests;

public sealed class TeacherScheduleAuthorizationTests
{
    [Fact]
    public async Task TeacherIsAuthorizedOnlyDuringScheduledLaboratoryWindow()
    {
        await using AppDbContext context = CreateContext();
        DateTime now = DateTime.Now;

        context.ClassSchedules.Add(new ClassSchedule
        {
            TeacherUserId = 10,
            LaboratoryId = 601,
            SubjectName = "Programming 2",
            ClassName = "BSIT-2A",
            ScheduleDate = now.Date,
            StartTime = now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5)),
            EndTime = now.TimeOfDay.Add(TimeSpan.FromMinutes(5)),
            Status = "Scheduled",
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var service = new TeacherScheduleService(context);

        Assert.True(await service.IsTeacherScheduledAsync(10, 601, now));
        Assert.False(await service.IsTeacherScheduledAsync(10, 603, now));
        Assert.False(await service.IsTeacherScheduledAsync(11, 601, now));
    }

    [Fact]
    public async Task TeacherIsRejectedOutsideScheduledTime()
    {
        await using AppDbContext context = CreateContext();
        DateTime now = DateTime.Now;

        context.ClassSchedules.Add(new ClassSchedule
        {
            TeacherUserId = 10,
            LaboratoryId = 601,
            SubjectName = "Programming 2",
            ClassName = "BSIT-2A",
            ScheduleDate = now.Date,
            StartTime = now.TimeOfDay.Add(TimeSpan.FromHours(1)),
            EndTime = now.TimeOfDay.Add(TimeSpan.FromHours(2)),
            Status = "Scheduled",
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var service = new TeacherScheduleService(context);

        Assert.False(await service.IsTeacherScheduledAsync(10, 601, now));
    }

    [Fact]
    public async Task CancelledScheduleDoesNotGrantAccess()
    {
        await using AppDbContext context = CreateContext();
        DateTime now = DateTime.Now;

        context.ClassSchedules.Add(new ClassSchedule
        {
            TeacherUserId = 10,
            LaboratoryId = 601,
            SubjectName = "Programming 2",
            ClassName = "BSIT-2A",
            ScheduleDate = now.Date,
            StartTime = now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5)),
            EndTime = now.TimeOfDay.Add(TimeSpan.FromMinutes(5)),
            Status = "Cancelled",
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var service = new TeacherScheduleService(context);

        Assert.False(await service.IsTeacherScheduledAsync(10, 601, now));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
