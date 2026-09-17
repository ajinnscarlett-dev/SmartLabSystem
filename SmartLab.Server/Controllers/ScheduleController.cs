using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Teacher")]
public sealed class ScheduleController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TeacherScheduleService _scheduleService;

    public ScheduleController(AppDbContext context, TeacherScheduleService scheduleService)
    {
        _context = context;
        _scheduleService = scheduleService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date)
    {
        DateTime targetDate = (date ?? DateTime.Now).Date;
        var schedules = await Project(_context.ClassSchedules.AsNoTracking()
                .Where(s => s.ScheduleDate >= targetDate && s.ScheduleDate < targetDate.AddDays(1)))
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.LaboratoryName)
            .ThenBy(s => s.TeacherName)
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetMine([FromQuery] DateTime? date)
    {
        if (!TryGetUserId(out int teacherId)) return Unauthorized();
        var schedules = await Project(_scheduleService.GetTeacherSchedules(teacherId, date))
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.LaboratoryName)
            .ToListAsync();
        return Ok(schedules);
    }

    [HttpGet("current")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetCurrent()
    {
        if (!TryGetUserId(out int teacherId)) return Unauthorized();
        var schedules = await Project(_scheduleService.GetTeacherSchedules(teacherId, DateTime.Now))
            .Where(s => s.Status == "Scheduled" && s.StartTime <= DateTime.Now.TimeOfDay && s.EndTime > DateTime.Now.TimeOfDay)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
        return Ok(schedules);
    }

    [HttpGet("laboratory/{laboratoryId}")]
    public async Task<IActionResult> GetLaboratory(int laboratoryId, [FromQuery] DateTime? date)
    {
        if (User.IsInRole("Teacher") && TryGetUserId(out int teacherId))
        {
            bool allowed = await _scheduleService.IsTeacherScheduledAsync(teacherId, laboratoryId);
            if (!allowed) return Forbid();
        }

        DateTime targetDate = (date ?? DateTime.Now).Date;
        var schedules = await Project(_context.ClassSchedules.AsNoTracking()
                .Where(s => s.LaboratoryId == laboratoryId && s.ScheduleDate >= targetDate && s.ScheduleDate < targetDate.AddDays(1)))
            .OrderBy(s => s.StartTime)
            .ToListAsync();
        return Ok(schedules);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] ScheduleWriteRequest request)
    {
        string validation = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validation)) return BadRequest(new { message = validation });

        User? teacher = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.TeacherUserId);
        if (teacher == null || !string.Equals(teacher.Role, "Teacher", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Selected user is not an active Teacher." });
        if (IsDisabled(teacher)) return BadRequest(new { message = "Selected Teacher account is inactive." });

        Laboratory? laboratory = await _context.Laboratories.FirstOrDefaultAsync(l => l.LaboratoryId == request.LaboratoryId);
        if (laboratory == null) return BadRequest(new { message = "Selected laboratory does not exist." });

        bool conflict = await HasConflictAsync(request, null);
        if (conflict) return Conflict(new { message = "The Teacher or laboratory already has an overlapping schedule." });

        var schedule = new ClassSchedule
        {
            TeacherUserId = request.TeacherUserId,
            LaboratoryId = request.LaboratoryId,
            SubjectName = request.SubjectName.Trim(),
            ClassName = request.ClassName.Trim(),
            ScheduleDate = request.ScheduleDate.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = NormalizeStatus(request.Status),
            CreatedAt = DateTime.Now
        };

        _context.ClassSchedules.Add(schedule);
        await _context.SaveChangesAsync();
        await LogAsync(teacher.UserId, laboratory.LaboratoryId, "Schedule Created", $"Schedule created for {teacher.Username} in {laboratory.LabName}.");

        return Ok(new { message = "Schedule created successfully.", scheduleId = schedule.ClassScheduleId });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] ScheduleWriteRequest request)
    {
        string validation = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validation)) return BadRequest(new { message = validation });

        ClassSchedule? schedule = await _context.ClassSchedules.FirstOrDefaultAsync(s => s.ClassScheduleId == id);
        if (schedule == null) return NotFound(new { message = "Schedule not found." });

        User? teacher = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.TeacherUserId);
        if (teacher == null || !string.Equals(teacher.Role, "Teacher", StringComparison.OrdinalIgnoreCase) || IsDisabled(teacher))
            return BadRequest(new { message = "Selected user is not an active Teacher." });

        if (!await _context.Laboratories.AnyAsync(l => l.LaboratoryId == request.LaboratoryId))
            return BadRequest(new { message = "Selected laboratory does not exist." });

        if (await HasConflictAsync(request, id))
            return Conflict(new { message = "The Teacher or laboratory already has an overlapping schedule." });

        schedule.TeacherUserId = request.TeacherUserId;
        schedule.LaboratoryId = request.LaboratoryId;
        schedule.SubjectName = request.SubjectName.Trim();
        schedule.ClassName = request.ClassName.Trim();
        schedule.ScheduleDate = request.ScheduleDate.Date;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.Status = NormalizeStatus(request.Status);

        await _context.SaveChangesAsync();
        await LogAsync(teacher.UserId, schedule.LaboratoryId, "Schedule Updated", $"Schedule #{id} was updated.");
        return Ok(new { message = "Schedule updated successfully." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        ClassSchedule? schedule = await _context.ClassSchedules.FirstOrDefaultAsync(s => s.ClassScheduleId == id);
        if (schedule == null) return NotFound(new { message = "Schedule not found." });

        _context.ClassSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        await LogAsync(schedule.TeacherUserId, schedule.LaboratoryId, "Schedule Deleted", $"Schedule #{id} was deleted.");
        return Ok(new { message = "Schedule deleted successfully." });
    }

    private async Task<bool> HasConflictAsync(ScheduleWriteRequest request, int? excludeId)
    {
        DateTime dayStart = request.ScheduleDate.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        return await _context.ClassSchedules.AnyAsync(s =>
            (!excludeId.HasValue || s.ClassScheduleId != excludeId.Value) &&
            s.ScheduleDate >= dayStart && s.ScheduleDate < dayEnd &&
            s.Status == "Scheduled" &&
            (s.TeacherUserId == request.TeacherUserId || s.LaboratoryId == request.LaboratoryId) &&
            s.StartTime < request.EndTime && request.StartTime < s.EndTime);
    }

    private static string ValidateRequest(ScheduleWriteRequest request)
    {
        if (request.TeacherUserId <= 0) return "Teacher is required.";
        if (request.LaboratoryId <= 0) return "Laboratory is required.";
        if (string.IsNullOrWhiteSpace(request.SubjectName)) return "Subject/Class subject is required.";
        if (string.IsNullOrWhiteSpace(request.ClassName)) return "Class name is required.";
        if (request.SubjectName.Trim().Length > 120) return "Subject name cannot exceed 120 characters.";
        if (request.ClassName.Trim().Length > 120) return "Class name cannot exceed 120 characters.";
        if (request.EndTime <= request.StartTime) return "End time must be later than start time.";
        return string.Empty;
    }

    private static string NormalizeStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "cancelled" => "Cancelled",
        "completed" => "Completed",
        _ => "Scheduled"
    };

    private static bool IsDisabled(User user) => user.Role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase) || user.Username.StartsWith("__SMARTLAB_DISABLED__|", StringComparison.Ordinal);

    private static IQueryable<ScheduleRow> Project(IQueryable<ClassSchedule> query)
    {
        return query.Select(s => new ScheduleRow
        {
            ScheduleId = s.ClassScheduleId,
            TeacherUserId = s.TeacherUserId,
            TeacherName = s.TeacherUser.FullName ?? s.TeacherUser.Username,
            TeacherUsername = s.TeacherUser.Username,
            LaboratoryId = s.LaboratoryId,
            LaboratoryName = s.Laboratory.LabName,
            SubjectName = s.SubjectName,
            ClassName = s.ClassName,
            ScheduleDate = s.ScheduleDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Status = s.Status
        });
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private async Task LogAsync(int? userId, int? laboratoryId, string action, string details)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            PCId = null,
            Action = action,
            Details = details,
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();
    }

    private sealed class ScheduleRow
    {
        public int ScheduleId { get; set; }
        public int TeacherUserId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string TeacherUsername { get; set; } = string.Empty;
        public int LaboratoryId { get; set; }
        public string LaboratoryName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime ScheduleDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = "Scheduled";
    }

    public sealed class ScheduleWriteRequest
    {
        public int TeacherUserId { get; set; }
        public int LaboratoryId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime ScheduleDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = "Scheduled";
    }
}