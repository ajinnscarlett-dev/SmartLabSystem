using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PC> PCs { get; set; }
        public DbSet<Laboratory> Laboratories { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<ServiceDeskTicket> ServiceDeskTickets { get; set; }
        public DbSet<TeacherLaboratoryAuthorization> TeacherLaboratoryAuthorizations { get; set; }
        public DbSet<PcUsageHistory> PcUsageHistory { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<HardwareInventory> HardwareInventories { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PC>()
                .HasIndex(p => p.PCNumber)
                .IsUnique();

            modelBuilder.Entity<PC>()
                .HasIndex(p => p.MACAddress)
                .IsUnique()
                .HasFilter("[MACAddress] IS NOT NULL");

            modelBuilder.Entity<PC>()
                .HasIndex(p => new { p.LaboratoryId, p.Status });

            modelBuilder.Entity<PC>()
                .HasIndex(p => p.LastSeen);

            modelBuilder.Entity<PcUsageHistory>()
                .HasIndex(s => new { s.LaboratoryId, s.LoginTime });

            modelBuilder.Entity<PcUsageHistory>()
                .HasIndex(s => new { s.PCId, s.LoginTime });

            modelBuilder.Entity<MaintenanceRecord>()
                .HasIndex(m => new { m.PCId, m.StartedAt });

            modelBuilder.Entity<HardwareInventory>()
                .HasIndex(h => h.PCId)
                .IsUnique();

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        }

        public override int SaveChanges()
        {
            PrepareOperationalHistory();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            PrepareOperationalHistory();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void PrepareOperationalHistory()
        {
            DateTime now = DateTime.Now;

            foreach (var entry in ChangeTracker.Entries<PC>())
            {
                if (entry.State != EntityState.Modified)
                {
                    continue;
                }

                string previousStatus =
                    entry.Property(p => p.Status).OriginalValue ?? string.Empty;

                string currentStatus =
                    entry.Entity.Status ?? string.Empty;

                int? previousUserId =
                    entry.Property(p => p.CurrentUserId).OriginalValue;

                int? currentUserId =
                    entry.Entity.CurrentUserId;

                bool enteredOccupied =
                    !string.Equals(previousStatus, "Occupied", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentStatus, "Occupied", StringComparison.OrdinalIgnoreCase) &&
                    currentUserId.HasValue;

                bool leftOccupied =
                    string.Equals(previousStatus, "Occupied", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentStatus, "Occupied", StringComparison.OrdinalIgnoreCase);

                if (enteredOccupied)
                {
                    bool hasOpenSession = PcUsageHistory.Any(s =>
                        s.PCId == entry.Entity.PCId &&
                        s.LogoutTime == null);

                    bool hasPendingOpenSession = ChangeTracker.Entries<PcUsageHistory>()
                        .Any(e =>
                            e.State == EntityState.Added &&
                            e.Entity.PCId == entry.Entity.PCId &&
                            e.Entity.LogoutTime == null);

                    if (!hasOpenSession && !hasPendingOpenSession)
                    {
                        PcUsageHistory.Add(new PcUsageHistory
                        {
                            PCId = entry.Entity.PCId,
                            UserId = currentUserId!.Value,
                            LaboratoryId = entry.Entity.LaboratoryId,
                            LoginTime = now,
                            EndReason = string.Empty
                        });
                    }
                }

                if (leftOccupied && previousUserId.HasValue)
                {
                    string endReason =
                        string.Equals(currentStatus, "Offline", StringComparison.OrdinalIgnoreCase)
                            ? "HeartbeatTimeout"
                            : string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase)
                                ? "Maintenance"
                                : "Logout";

                    PcUsageHistory? openSession = PcUsageHistory
                        .OrderByDescending(s => s.LoginTime)
                        .FirstOrDefault(s =>
                            s.PCId == entry.Entity.PCId &&
                            s.LogoutTime == null);

                    if (openSession != null)
                    {
                        CloseUsageSession(openSession, now, endReason);
                    }
                }

                bool enteredMaintenance =
                    !string.Equals(previousStatus, "Maintenance", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);

                bool leftMaintenance =
                    string.Equals(previousStatus, "Maintenance", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);

                if (enteredMaintenance)
                {
                    bool alreadyOpen = MaintenanceRecords.Any(m =>
                        m.PCId == entry.Entity.PCId &&
                        m.EndedAt == null);

                    if (!alreadyOpen)
                    {
                        MaintenanceRecords.Add(new MaintenanceRecord
                        {
                            PCId = entry.Entity.PCId,
                            Reason = entry.Entity.MaintenanceReason?.Trim() ?? "Maintenance",
                            StartedAt = entry.Entity.MaintenanceStarted ?? now,
                            Notes = null,
                            TechnicianUserId = null
                        });
                    }
                }

                if (leftMaintenance)
                {
                    MaintenanceRecord? openMaintenance = MaintenanceRecords
                        .OrderByDescending(m => m.StartedAt)
                        .FirstOrDefault(m =>
                            m.PCId == entry.Entity.PCId &&
                            m.EndedAt == null);

                    if (openMaintenance != null)
                    {
                        openMaintenance.EndedAt = now;
                    }
                }
            }
        }

        private static void CloseUsageSession(
            PcUsageHistory session,
            DateTime logoutTime,
            string endReason)
        {
            session.LogoutTime = logoutTime;
            session.EndReason = endReason;

            TimeSpan duration = logoutTime - session.LoginTime;

            session.DurationSeconds =
                (int)Math.Max(0, Math.Round(duration.TotalSeconds));
        }
    }
}
