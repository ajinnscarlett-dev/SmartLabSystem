namespace SmartLab.Server
{
    public class ActivityLog
    {
        public int ActivityLogId { get; set; }

        public int? PCId { get; set; }

        public int? UserId { get; set; }

        public string Action { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public PC? PC { get; set; }

        public User? User { get; set; }
    }
}