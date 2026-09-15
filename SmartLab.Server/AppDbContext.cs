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
    }
}
