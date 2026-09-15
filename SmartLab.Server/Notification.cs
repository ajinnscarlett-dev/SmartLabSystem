namespace SmartLab.Server
{
    public class Notification
    {
        public long NotificationId { get; set; }
        public int UserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReadAt { get; set; }
        public bool IsRead { get; set; }

        public User? User { get; set; }
    }
}
