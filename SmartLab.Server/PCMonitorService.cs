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

            _timeout = TimeSpan.FromSeconds(
                Math.Max(5, timeoutSeconds));

            _checkInterval = TimeSpan.FromSeconds(
                Math.Max(1, intervalSeconds));
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckPCs(stoppingToken);
                }
                catch (OperationCanceledException) when (
                    stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"PC Monitor Error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(
                        _checkInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CheckPCs(
            CancellationToken cancellationToken)
        {
            using IServiceScope scope =
                _scopeFactory.CreateScope();

            AppDbContext context =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            DateTime cutoffTime =
                DateTime.Now - _timeout;

            // Maintenance is an explicit administrative state and must never
            // be changed by automatic presence monitoring.
            var stalePCs = await context.PCs
                .Where(p =>
                    p.Status != "Maintenance" &&
                    p.LastSeen != null &&
                    p.LastSeen < cutoffTime)
                .ToListAsync(cancellationToken);

            if (stalePCs.Count == 0)
            {
                return;
            }

            foreach (PC pc in stalePCs)
            {
                string previousStatus = pc.Status;

                // A stale occupied PC has lost its live session ownership.
                // History persistence is handled by the session subsystem;
                // this monitor only reconciles the operational PC state.
                pc.CurrentUserId = null;
                pc.Status = "Offline";

                Console.WriteLine(
                    $"[PC MONITOR] {pc.PCNumber} " +
                    $"{previousStatus} -> Offline. " +
                    $"Last heartbeat: {pc.LastSeen:O}");

                context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = null,
                    PCId = pc.PCId,
                    Action = "PC Offline",
                    Details =
                        $"PC {pc.PCNumber} changed from {previousStatus} to Offline after heartbeat timeout.",
                    CreatedAt = DateTime.Now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
