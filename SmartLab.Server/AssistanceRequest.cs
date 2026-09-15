namespace SmartLab.Server
{
    public class AssistanceRequest
    {
        public long AssistanceRequestId { get; set; }
        public int StudentUserId { get; set; }
        public int PCId { get; set; }
        public int? LaboratoryId { get; set; }
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
        public string? ResolutionNotes { get; set; }

        public User? StudentUser { get; set; }
        public PC? PC { get; set; }
        public Laboratory? Laboratory { get; set; }
        public User? ResolvedByUser { get; set; }
    }
}
