using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
        public DbSet<AssistanceRequest> AssistanceRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PC>().HasIndex(p => p.PCNumber).IsUnique();
            modelBuilder.Entity<PC>().HasIndex(p => p.MACAddress).IsUnique().HasFilter("[MACAddress] IS NOT NULL");
            modelBuilder.Entity<PC>().HasIndex(p => new { p.LaboratoryId, p.Status });
            modelBuilder.Entity<PC>().HasIndex(p => p.LastSeen);
            modelBuilder.Entity<PcUsageHistory>().HasKey(s => s.SessionId);
            modelBuilder.Entity<PcUsageHistory>().HasIndex(s => new { s.LaboratoryId, s.LoginTime });
            modelBuilder.Entity<PcUsageHistory>().HasIndex(s => new { s.PCId, s.LoginTime });
            modelBuilder.Entity<MaintenanceRecord>().HasIndex(m => new { m.PCId, m.StartedAt });
            modelBuilder.Entity<HardwareInventory>().HasIndex(h => h.PCId).IsUnique();
            modelBuilder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            modelBuilder.Entity<AssistanceRequest>().HasIndex(a => new { a.LaboratoryId, a.CreatedAt });
            modelBuilder.Entity<AssistanceRequest>().HasIndex(a => new { a.StudentUserId, a.CreatedAt });
            modelBuilder.Entity<AssistanceRequest>().HasIndex(a => a.PCId);

            modelBuilder.Entity<PcUsageHistory>().HasOne(s => s.PC).WithMany().HasForeignKey(s => s.PCId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PcUsageHistory>().HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PcUsageHistory>().HasOne(s => s.Laboratory).WithMany().HasForeignKey(s => s.LaboratoryId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MaintenanceRecord>().HasOne(m => m.PC).WithMany().HasForeignKey(m => m.PCId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MaintenanceRecord>().HasOne(m => m.TechnicianUser).WithMany().HasForeignKey(m => m.TechnicianUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<HardwareInventory>().HasOne(h => h.PC).WithMany().HasForeignKey(h => h.PCId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssistanceRequest>().HasOne(a => a.StudentUser).WithMany().HasForeignKey(a => a.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssistanceRequest>().HasOne(a => a.PC).WithMany().HasForeignKey(a => a.PCId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssistanceRequest>().HasOne(a => a.Laboratory).WithMany().HasForeignKey(a => a.LaboratoryId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssistanceRequest>().HasOne(a => a.ResolvedByUser).WithMany().HasForeignKey(a => a.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        }

        public override int SaveChanges()
        {
            PrepareOperationalHistory();
            PrepareNotifications();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            PrepareOperationalHistory();
            PrepareNotifications();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void PrepareOperationalHistory()
        {
            DateTime now = DateTime.Now;
            foreach (var entry in ChangeTracker.Entries<PC>().ToList())
            {
                if (entry.State != EntityState.Modified) continue;

                string previousStatus = entry.Property(p => p.Status).OriginalValue ?? string.Empty;
                string currentStatus = entry.Entity.Status ?? string.Empty;
                int? previousUserId = entry.Property(p => p.CurrentUserId).OriginalValue;
                int? currentUserId = entry.Entity.CurrentUserId;

                bool enteredOccupied = !string.Equals(previousStatus, "Occupied", StringComparison.OrdinalIgnoreCase) && string.Equals(currentStatus, "Occupied", StringComparison.OrdinalIgnoreCase) && currentUserId.HasValue;
                bool leftOccupied = string.Equals(previousStatus, "Occupied", StringComparison.OrdinalIgnoreCase) && !string.Equals(currentStatus, "Occupied", StringComparison.OrdinalIgnoreCase);

                if (enteredOccupied)
                {
                    bool hasOpenSession = PcUsageHistory.Any(s => s.PCId == entry.Entity.PCId && s.LogoutTime == null);
                    bool hasPendingOpenSession = ChangeTracker.Entries<PcUsageHistory>().Any(e => e.State == EntityState.Added && e.Entity.PCId == entry.Entity.PCId && e.Entity.LogoutTime == null);
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
                    string endReason = string.Equals(currentStatus, "Offline", StringComparison.OrdinalIgnoreCase)
                        ? "HeartbeatTimeout"
                        : string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase)
                            ? "Maintenance"
                            : "Logout";

                    PcUsageHistory? openSession = PcUsageHistory.OrderByDescending(s => s.LoginTime).FirstOrDefault(s => s.PCId == entry.Entity.PCId && s.LogoutTime == null);
                    if (openSession != null) CloseUsageSession(openSession, now, endReason);
                }

                bool enteredMaintenance = !string.Equals(previousStatus, "Maintenance", StringComparison.OrdinalIgnoreCase) && string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);
                bool leftMaintenance = string.Equals(previousStatus, "Maintenance", StringComparison.OrdinalIgnoreCase) && !string.Equals(currentStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);

                if (enteredMaintenance)
                {
                    bool alreadyOpen = MaintenanceRecords.Any(m => m.PCId == entry.Entity.PCId && m.EndedAt == null);
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
                    MaintenanceRecord? openMaintenance = MaintenanceRecords.OrderByDescending(m => m.StartedAt).FirstOrDefault(m => m.PCId == entry.Entity.PCId && m.EndedAt == null);
                    if (openMaintenance != null) openMaintenance.EndedAt = now;
                }
            }
        }

        private void PrepareNotifications()
        {
            DateTime now = DateTime.Now;

            foreach (var entry in ChangeTracker.Entries<Announcement>().ToList())
            {
                if (entry.State != EntityState.Added) continue;
                string targetRole = entry.Entity.TargetRole?.Trim() ?? "All";
                var recipients = Users.Where(u => targetRole == "All" || u.Role == targetRole).Select(u => u.UserId).ToList();
                foreach (int userId in recipients)
                    Notifications.Add(new Notification { UserId = userId, Type = "Announcement", Title = entry.Entity.Title, Message = entry.Entity.Message, CreatedAt = now, IsRead = false });
            }

            foreach (var entry in ChangeTracker.Entries<ServiceDeskTicket>().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    foreach (int userId in Users.Where(u => u.Role == "Admin").Select(u => u.UserId).ToList())
                        Notifications.Add(new Notification { UserId = userId, Type = "ServiceDesk", Title = "New Service Desk Ticket", Message = $"{entry.Entity.Subject} ({entry.Entity.Category})", CreatedAt = now, IsRead = false });
                }
                else if (entry.State == EntityState.Modified && entry.Property(t => t.Status).IsModified)
                {
                    string oldStatus = entry.Property(t => t.Status).OriginalValue ?? string.Empty;
                    string newStatus = entry.Entity.Status ?? string.Empty;
                    if (!string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                        Notifications.Add(new Notification { UserId = entry.Entity.TeacherUserId, Type = "ServiceDesk", Title = "Service Desk Updated", Message = $"Ticket #{entry.Entity.ServiceDeskTicketId} is now {newStatus}.", CreatedAt = now, IsRead = false });
                }
            }

            foreach (var entry in ChangeTracker.Entries<AssistanceRequest>().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    var recipientIds = Users.Where(u => u.Role == "Admin").Select(u => u.UserId).ToList();
                    if (entry.Entity.LaboratoryId.HasValue)
                    {
                        var teacherIds = TeacherLaboratoryAuthorizations
                            .Where(a => a.LaboratoryId == entry.Entity.LaboratoryId.Value)
                            .Select(a => a.TeacherUserId)
                            .Distinct()
                            .ToList();
                        recipientIds.AddRange(teacherIds);
                    }

                    foreach (int userId in recipientIds.Distinct())
                        Notifications.Add(new Notification { UserId = userId, Type = "NeedAssistance", Title = "Student Needs Assistance", Message = $"PC #{entry.Entity.PCId}: {entry.Entity.Description}", CreatedAt = now, IsRead = false });
                }
                else if (entry.State == EntityState.Modified && entry.Property(a => a.Status).IsModified)
                {
                    string oldStatus = entry.Property(a => a.Status).OriginalValue ?? string.Empty;
                    string newStatus = entry.Entity.Status ?? string.Empty;
                    if (!string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                        Notifications.Add(new Notification { UserId = entry.Entity.StudentUserId, Type = "NeedAssistance", Title = "Assistance Request Updated", Message = $"Your assistance request #{entry.Entity.AssistanceRequestId} is now {newStatus}.", CreatedAt = now, IsRead = false });
                }
            }
        }

        private static void CloseUsageSession(PcUsageHistory session, DateTime logoutTime, string endReason)
        {
            session.LogoutTime = logoutTime;
            session.EndReason = endReason;
            session.DurationSeconds = (int)Math.Max(0, Math.Round((logoutTime - session.LoginTime).TotalSeconds));
        }
    }
}
