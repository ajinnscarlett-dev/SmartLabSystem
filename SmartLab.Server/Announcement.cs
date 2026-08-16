using System;

namespace SmartLab.Server
{
    public class Announcement
    {
        public int AnnouncementId { get; set; }

        // ==========================================
        // ANNOUNCEMENT CONTENT
        // ==========================================

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;


        // ==========================================
        // ADMIN WHO POSTED IT
        // ==========================================

        public int? PostedByUserId { get; set; }


        // ==========================================
        // DATE / TIME
        // ==========================================

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;


        // ==========================================
        // ACTIVE / INACTIVE
        // ==========================================

        public bool IsActive { get; set; } = true;


        // ==========================================
        // OPTIONAL EXPIRATION
        // ==========================================

        public DateTime? ExpiresAt { get; set; }
    }
}