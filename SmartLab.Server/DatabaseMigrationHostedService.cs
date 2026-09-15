using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public sealed class DatabaseMigrationHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;

        public DatabaseMigrationHostedService(IServiceProvider services, IConfiguration configuration)
        {
            _services = services;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            bool applyMigrations = _configuration.GetValue("SmartLab:ApplyMigrationsOnStartup", true);
            if (!applyMigrations)
                return;

            using IServiceScope scope = _services.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.MigrateAsync(cancellationToken);

            // Development-only first-admin setup. Credentials must come from
            // local environment/user-secrets configuration, never source control.
            if (!_configuration.GetValue("SmartLab:EnableDevelopmentBootstrap", false))
                return;

            string bootstrapUsername =
                _configuration["SmartLab:DevelopmentAdminUsername"]?.Trim()
                ?? Environment.GetEnvironmentVariable("SMARTLAB_DEV_ADMIN_USERNAME")?.Trim()
                ?? string.Empty;

            string bootstrapPassword =
                _configuration["SmartLab:DevelopmentAdminPassword"]
                ?? Environment.GetEnvironmentVariable("SMARTLAB_DEV_ADMIN_PASSWORD")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(bootstrapUsername) || string.IsNullOrWhiteSpace(bootstrapPassword))
                return;

            if (bootstrapPassword.Length < 6)
                throw new InvalidOperationException("Development admin password must be at least 6 characters long.");

            var passwordHasher = new PasswordHasher<User>();
            User? admin = await context.Users.FirstOrDefaultAsync(
                u => u.Username == bootstrapUsername,
                cancellationToken);

            if (admin == null)
            {
                admin = new User
                {
                    Username = bootstrapUsername,
                    Role = "Admin",
                    CreatedAt = DateTime.Now
                };

                admin.PasswordHash = passwordHasher.HashPassword(admin, bootstrapPassword);
                context.Users.Add(admin);
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (!admin.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, bootstrapPassword) == PasswordVerificationResult.Failed)
            {
                admin.Role = "Admin";
                admin.PasswordHash = passwordHasher.HashPassword(admin, bootstrapPassword);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
