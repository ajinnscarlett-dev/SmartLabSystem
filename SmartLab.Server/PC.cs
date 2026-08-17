namespace SmartLab.Server
{
    public class PC
    {
        public int PCId { get; set; }

        public string PCNumber { get; set; } =
            string.Empty;

        public string Status { get; set; } =
            "Available";

        public int? CurrentUserId { get; set; }

        public DateTime? LastSeen { get; set; }

        // ==========================================
        // LABORATORY / COMLAB
        // ==========================================

        public int? LaboratoryId { get; set; }

        public Laboratory? Laboratory { get; set; }

        // ==========================================
        // PC NETWORK IDENTITY
        // ==========================================

        public string? MACAddress { get; set; }

        public string? IPAddress { get; set; }

        // ==========================================
        // ENABLE / DISABLE
        // ==========================================

        public bool IsEnabled { get; set; } =
            true;

        // ==========================================
        // MAINTENANCE
        // ==========================================

        public string? MaintenanceReason { get; set; }

        public DateTime? MaintenanceStarted { get; set; }

        // ==========================================
        // CURRENT USER
        // ==========================================

        public User? CurrentUser { get; set; }
    }
}
