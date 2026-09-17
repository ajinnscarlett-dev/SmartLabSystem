namespace SmartLab.Server
{
    public class User
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool MustChangePassword { get; set; } = true;

        public DateTime CreatedAt { get; set; }
    }
}