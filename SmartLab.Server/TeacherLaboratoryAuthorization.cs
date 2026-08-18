namespace SmartLab.Server
{
    public class TeacherLaboratoryAuthorization
    {
        public int TeacherLaboratoryAuthorizationId { get; set; }

        public int TeacherUserId { get; set; }

        public int LaboratoryId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? TeacherUser { get; set; }

        public Laboratory? Laboratory { get; set; }
    }
}
