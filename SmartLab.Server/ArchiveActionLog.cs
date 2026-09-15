namespace SmartLab.Server
{
    public class ArchiveActionLog
    {
        public long ArchiveActionLogId { get; set; }
        public int RequestedByUserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int AgeDays { get; set; }
        public DateTime CutoffAt { get; set; }
        public int UsageCount { get; set; }
        public int MaintenanceCount { get; set; }
        public int ServiceDeskCount { get; set; }
        public int ActivityLogCount { get; set; }
        public string Format { get; set; } = "ZIP/CSV";
        public string? OutputFileName { get; set; }
        public string? OutputSha256 { get; set; }
        public string Status { get; set; } = "Started";
        public string? Notes { get; set; }
        public User? RequestedByUser { get; set; }
    }
}
