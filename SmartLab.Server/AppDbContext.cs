using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<PC> PCs { get; set; }

        public DbSet<Laboratory> Laboratories
        { get; set; }

        public DbSet<ActivityLog> ActivityLogs
        { get; set; }

        public DbSet<Announcement> Announcements
        { get; set; }

        public DbSet<ServiceDeskTicket>
            ServiceDeskTickets
        { get; set; }

        public DbSet<TeacherLaboratoryAuthorization>
            TeacherLaboratoryAuthorizations
        { get; set; }
    }
}
