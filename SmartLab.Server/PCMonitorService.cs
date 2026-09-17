using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public sealed class PCMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _checkInterval;

        public PCMonitorService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;

            int timeoutSeconds = configuration.GetValue(
                "SmartLab:HeartbeatTimeoutSeconds",
                20);

            int intervalSeconds = configuration.GetValue(
                "SmartLab:MonitorIntervalSeconds",
                5);

            _timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
            _checkInterval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));
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
                    await Task.Delay(_checkInterval, stoppingToken);
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
            DateTime cutoff = DateTime.Now - _timeout;

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

                // Keep CurrentUserId during a temporary heartbeat outage so the
                // authenticated student's heartbeat can restore the PC when the
                // network connection returns. The ownership check remains in the
                // request-integrity filter, so another student cannot claim it.
                pc.Status = "Offline";

                if (!string.Equals(previousStatus, pc.Status, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[PC MONITOR] {pc.PCNumber}: {previousStatus} -> Offline");

                    context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = null,
                        PCId = pc.PCId,
                        Action = "PC Status Changed",
                        Details = $"PC {pc.PCNumber} changed from {previousStatus} to Offline after heartbeat timeout. Student ownership was retained for reconnect recovery.",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
