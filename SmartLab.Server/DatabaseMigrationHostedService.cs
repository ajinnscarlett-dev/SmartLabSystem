using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public sealed class DatabaseMigrationHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;

        public DatabaseMigrationHostedService(
            IServiceProvider services,
            IConfiguration configuration)
        {
            _services = services;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            bool applyMigrations = _configuration.GetValue(
                "SmartLab:ApplyMigrationsOnStartup",
                true);

            if (!applyMigrations)
            {
                return;
            }

            using IServiceScope scope = _services.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.MigrateAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
