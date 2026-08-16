using System;

namespace SmartLab.Server
{
    public class ServiceDeskTicket
    {
        public int ServiceDeskTicketId { get; set; }


        // ==========================================
        // TEACHER REQUESTER
        // ==========================================

        public int TeacherUserId { get; set; }

        public string TeacherUsername { get; set; } =
            string.Empty;


        // ==========================================
        // LOCATION / PC
        // ==========================================

        public string? PCNumber { get; set; }

        public string? Location { get; set; }


        // ==========================================
        // REQUEST
        // ==========================================

        public string Category { get; set; } =
            string.Empty;

        public string Subject { get; set; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;


        // ==========================================
        // STATUS
        // ==========================================

        public string Status { get; set; } =
            "Open";


        // ==========================================
        // MIS ASSIGNMENT
        // ==========================================

        public int? AssignedToUserId { get; set; }

        public string? AssignedToUsername { get; set; }


        // ==========================================
        // DATE / TIME
        // ==========================================

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;

        public DateTime? StartedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }


        // ==========================================
        // MIS NOTES
        // ==========================================

        public string? ResolutionNotes { get; set; }
    }
}