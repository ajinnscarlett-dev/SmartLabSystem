namespace SmartLab.Server
{
    public class HardwareInventory
    {
        public int HardwareInventoryId { get; set; }
        public int PCId { get; set; }
        public string? Cpu { get; set; }
        public string? Ram { get; set; }
        public string? Storage { get; set; }
        public string? Gpu { get; set; }
        public string? OperatingSystem { get; set; }
        public string? MACAddress { get; set; }
        public string? IPAddress { get; set; }
        public DateTime LastAuditedAt { get; set; }

        public PC? PC { get; set; }
    }
}
