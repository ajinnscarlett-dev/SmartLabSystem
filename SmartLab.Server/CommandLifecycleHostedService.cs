using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Reflection;

namespace SmartLab.Server
{
    public sealed class CommandLifecycleHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _commandTtl;
        private readonly TimeSpan _cleanupInterval;

        public CommandLifecycleHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;

            _commandTtl = TimeSpan.FromSeconds(
                Math.Clamp(
                    configuration.GetValue("SmartLab:CommandTtlSeconds", 20),
                    5,
                    300));

            _cleanupInterval = TimeSpan.FromSeconds(5);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Command lifecycle error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            Type controllerType = typeof(Controllers.PCCommandController);

            FieldInfo? commandsField = controllerType.GetField(
                "Commands",
                BindingFlags.Static | BindingFlags.NonPublic);

            FieldInfo? pendingField = controllerType.GetField(
                "PendingCommandByPc",
                BindingFlags.Static | BindingFlags.NonPublic);

            FieldInfo? remoteSessionsField = controllerType.GetField(
                "RemoteControlSessions",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (commandsField?.GetValue(null) is not IDictionary commands ||
                pendingField?.GetValue(null) is not IDictionary pending)
            {
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            DateTime cutoff = DateTime.Now - _commandTtl;

            foreach (DictionaryEntry entry in commands)
            {
                object command = entry.Value!;
                Type commandType = command.GetType();

                PropertyInfo? statusProperty = commandType.GetProperty("Status");
                PropertyInfo? createdProperty = commandType.GetProperty("CreatedAt");
                PropertyInfo? pcIdProperty = commandType.GetProperty("PCId");
                PropertyInfo? commandTypeProperty = commandType.GetProperty("CommandType");

                if (statusProperty?.GetValue(command) is not string status ||
                    !status.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
                    createdProperty?.GetValue(command) is not DateTime createdAt ||
                    pcIdProperty?.GetValue(command) is not int pcId)
                {
                    continue;
                }

                if (createdAt >= cutoff)
                {
                    continue;
                }

                statusProperty.SetValue(command, "TimedOut");
                commandType.GetProperty("CompletedAt")?.SetValue(command, DateTime.Now);
                commandType.GetProperty("Result")?.SetValue(command, "Command expired before client acknowledgement.");

                commands.Remove(entry.Key);
                pending.Remove(pcId);

                string commandName =
                    commandTypeProperty?.GetValue(command)?.ToString() ?? "UNKNOWN";

                context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = null,
                    PCId = pcId,
                    Action = "PC Command Timeout",
                    Details = $"{commandName} command expired after {_commandTtl.TotalSeconds:0} seconds.",
                    CreatedAt = DateTime.Now
                });
            }

            if (remoteSessionsField?.GetValue(null) is IDictionary remoteSessions)
            {
                var offlinePcIds = await context.PCs
                    .AsNoTracking()
                    .Where(p => p.Status == "Offline")
                    .Select(p => p.PCId)
                    .ToListAsync(cancellationToken);

                foreach (int pcId in offlinePcIds)
                {
                    if (!remoteSessions.Contains(pcId))
                        continue;

                    remoteSessions.Remove(pcId);

                    context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = null,
                        PCId = pcId,
                        Action = "Remote Control Cleanup",
                        Details = "Remote-control session removed because the PC is offline.",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
