using Microsoft.EntityFrameworkCore;

namespace SmartLab.Server;

public sealed class TeacherScheduleService
{
    private readonly AppDbContext _context;

    public TeacherScheduleService(AppDbContext context) => _context = context;

    public Task<bool> IsTeacherScheduledAsync(int teacherUserId, int laboratoryId, DateTime? at = null)
    {
        DateTime now = at ?? DateTime.Now;
        DateTime dayStart = now.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        TimeSpan time = now.TimeOfDay;

        return _context.ClassSchedules.AsNoTracking().AnyAsync(s =>
            s.TeacherUserId == teacherUserId &&
            s.LaboratoryId == laboratoryId &&
            s.ScheduleDate >= dayStart &&
            s.ScheduleDate < dayEnd &&
            s.Status == "Scheduled" &&
            s.StartTime <= time &&
            s.EndTime > time);
    }

    public IQueryable<ClassSchedule> GetTeacherSchedules(int teacherUserId, DateTime? date = null)
    {
        DateTime targetDate = (date ?? DateTime.Now).Date;
        DateTime nextDate = targetDate.AddDays(1);

        return _context.ClassSchedules
            .AsNoTracking()
            .Where(s => s.TeacherUserId == teacherUserId &&
                        s.ScheduleDate >= targetDate &&
                        s.ScheduleDate < nextDate);
    }

    public IQueryable<ClassSchedule> GetCurrentSchedules(DateTime? at = null)
    {
        DateTime now = at ?? DateTime.Now;
        DateTime dayStart = now.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        TimeSpan time = now.TimeOfDay;

        return _context.ClassSchedules
            .AsNoTracking()
            .Where(s => s.ScheduleDate >= dayStart &&
                        s.ScheduleDate < dayEnd &&
                        s.Status == "Scheduled" &&
                        s.StartTime <= time &&
                        s.EndTime > time);
    }
}