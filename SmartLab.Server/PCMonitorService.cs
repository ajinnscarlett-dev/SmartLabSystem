using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public sealed class PCMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        public PCMonitorService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckPCs(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PC Monitor Error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CheckPCs(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DateTime cutoff = DateTime.Now - Timeout;

            var stalePCs = await context.PCs
                .Where(p => p.Status != "Maintenance" &&
                            p.LastSeen != null &&
                            p.LastSeen < cutoff)
                .ToListAsync(cancellationToken);

            if (stalePCs.Count == 0)
            {
                return;
            }

            foreach (PC pc in stalePCs)
            {
                string previousStatus = pc.Status;

                if (pc.Status == "Occupied" && pc.CurrentUserId != null)
                {
                    pc.CurrentUserId = null;
                }

                pc.Status = "Offline";

                if (!string.Equals(previousStatus, pc.Status, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[PC MONITOR] {pc.PCNumber}: {previousStatus} -> Offline");
                    context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = null,
                        PCId = pc.PCId,
                        Action = "PC Status Changed",
                        Details = $"PC {pc.PCNumber} changed from {previousStatus} to Offline after heartbeat timeout.",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
