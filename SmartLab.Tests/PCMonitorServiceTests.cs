using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartLab.Server;
using Xunit;

namespace SmartLab.Tests;

public sealed class PCMonitorServiceTests
{
    [Fact]
    public async Task OccupiedPcTimeoutMarksOfflineWithoutClearingStudentOwnership()
    {
        string databaseName = nameof(OccupiedPcTimeoutMarksOfflineWithoutClearingStudentOwnership);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        await using ServiceProvider provider = services.BuildServiceProvider();

        using (IServiceScope seedScope = provider.CreateScope())
        {
            AppDbContext context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(new User
            {
                UserId = 7,
                Username = "student01",
                PasswordHash = "hash",
                Role = "Student"
            });

            context.PCs.Add(new PC
            {
                PCId = 70,
                PCNumber = "SCARLET",
                Status = "Occupied",
                CurrentUserId = 7,
                LastSeen = DateTime.Now.AddSeconds(-30),
                IsEnabled = true
            });

            await context.SaveChangesAsync();
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmartLab:HeartbeatTimeoutSeconds"] = "5",
                ["SmartLab:MonitorIntervalSeconds"] = "1"
            })
            .Build();

        var monitor = new PCMonitorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration);

        await monitor.StartAsync(CancellationToken.None);

        PC? monitoredPc = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(50);

            using IServiceScope verifyScope = provider.CreateScope();
            AppDbContext context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            monitoredPc = await context.PCs.SingleAsync(p => p.PCId == 70);

            if (monitoredPc.Status == "Offline")
            {
                break;
            }
        }

        await monitor.StopAsync(CancellationToken.None);

        Assert.NotNull(monitoredPc);
        Assert.Equal("Offline", monitoredPc!.Status);
        Assert.Equal(7, monitoredPc.CurrentUserId);
    }
}
