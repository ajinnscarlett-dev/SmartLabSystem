using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server;

public sealed class TeacherScheduleAuthorizationCacheService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(3);

    public TeacherScheduleAuthorizationCacheService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SynchronizeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Teacher schedule authorization sync error: {ex.Message}");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        DateTime now = DateTime.Now;
        DateTime dayStart = now.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        TimeSpan time = now.TimeOfDay;

        var desired = await context.ClassSchedules
            .AsNoTracking()
            .Where(s => s.ScheduleDate >= dayStart &&
                        s.ScheduleDate < dayEnd &&
                        s.Status == "Scheduled" &&
                        s.StartTime <= time &&
                        s.EndTime > time)
            .Select(s => new { s.TeacherUserId, s.LaboratoryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var desiredKeys = desired
            .Select(x => (x.TeacherUserId, x.LaboratoryId))
            .ToHashSet();

        var existing = await context.TeacherLaboratoryAuthorizations
            .ToListAsync(cancellationToken);

        bool changed = false;

        foreach (TeacherLaboratoryAuthorization authorization in existing)
        {
            if (!desiredKeys.Contains((authorization.TeacherUserId, authorization.LaboratoryId)))
            {
                context.TeacherLaboratoryAuthorizations.Remove(authorization);
                changed = true;
            }
        }

        var existingKeys = existing
            .Select(x => (x.TeacherUserId, x.LaboratoryId))
            .ToHashSet();

        foreach (var desiredPair in desiredKeys)
        {
            if (existingKeys.Contains(desiredPair))
                continue;

            context.TeacherLaboratoryAuthorizations.Add(new TeacherLaboratoryAuthorization
            {
                TeacherUserId = desiredPair.TeacherUserId,
                LaboratoryId = desiredPair.LaboratoryId,
                CreatedAt = now
            });
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken);
    }
}
