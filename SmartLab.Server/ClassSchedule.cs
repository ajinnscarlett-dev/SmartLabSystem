namespace SmartLab.Server;

public sealed class ClassSchedule
{
    public int ClassScheduleId { get; set; }

    public int TeacherUserId { get; set; }
    public User TeacherUser { get; set; } = null!;

    public int LaboratoryId { get; set; }
    public Laboratory Laboratory { get; set; } = null!;

    public string SubjectName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public DateTime ScheduleDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Status { get; set; } = "Scheduled";

    public DateTime CreatedAt { get; set; }
}