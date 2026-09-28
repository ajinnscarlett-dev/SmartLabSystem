namespace SmartLab.Server
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? StudentNumber { get; set; }
        public string? FullName { get; set; }
        public bool MustChangePassword { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}