namespace SmartLab.Server
{
    public class Announcement
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? PostedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresAt { get; set; }
        public string TargetRole { get; set; } = "All";
    }
}
