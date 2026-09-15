namespace SmartLab.Server
{
    public class PcUsageHistory
    {
        public long SessionId { get; set; }
        public int PCId { get; set; }
        public int UserId { get; set; }
        public int? LaboratoryId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
        public int? DurationSeconds { get; set; }
        public string EndReason { get; set; } = string.Empty;

        public PC? PC { get; set; }
        public User? User { get; set; }
        public Laboratory? Laboratory { get; set; }
    }
}
