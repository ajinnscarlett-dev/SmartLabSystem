using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public class PCMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        // PC is considered offline after 20 seconds
        // without receiving a heartbeat.
        private readonly TimeSpan _timeout =
            TimeSpan.FromSeconds(20);

        // Check PCs every 5 seconds.
        private readonly TimeSpan _checkInterval =
            TimeSpan.FromSeconds(5);


        public PCMonitorService(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
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
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"PC Monitor Error: {ex.Message}"
                    );
                }

                await Task.Delay(
                    _checkInterval,
                    stoppingToken
                );
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


            // Find occupied PCs whose heartbeat
            // has not been received recently.

            var stalePCs =
                await context.PCs
                    .Where(p =>
                        p.Status == "Occupied" &&
                        p.CurrentUserId != null &&
                        p.LastSeen != null &&
                        p.LastSeen < cutoffTime
                    )
                    .ToListAsync(
                        cancellationToken
                    );


            if (stalePCs.Count == 0)
            {
                return;
            }


            foreach (var pc in stalePCs)
            {
                Console.WriteLine(
                    $"[PC MONITOR] " +
                    $"{pc.PCNumber} heartbeat timeout. " +
                    $"Releasing PC."
                );


                // Release the PC

                pc.Status = "Available";

                pc.CurrentUserId = null;

                pc.LastSeen = DateTime.Now;
            }


            await context.SaveChangesAsync(
                cancellationToken
            );


            Console.WriteLine(
                $"[PC MONITOR] " +
                $"{stalePCs.Count} PC(s) automatically released."
            );
        }
    }
}