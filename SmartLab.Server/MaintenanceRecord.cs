namespace SmartLab.Server
{
    public class MaintenanceRecord
    {
        public long MaintenanceRecordId { get; set; }
        public int PCId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int? TechnicianUserId { get; set; }
        public string? Notes { get; set; }

        public PC? PC { get; set; }
        public User? TechnicianUser { get; set; }
    }
}
