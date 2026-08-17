namespace SmartLab.Server
{
    public class Laboratory
    {
        public int LaboratoryId { get; set; }

        public string LabName { get; set; } =
            string.Empty;

        public string? Description { get; set; }

        public ICollection<PC> PCs { get; set; } =
            new List<PC>();
    }
}
