using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeacherAuthorizationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TeacherAuthorizationController(
            AppDbContext context)
        {
            _context = context;
        }

        // ==========================================================
        // GET MY AUTHORIZED COMLABS
        // ==========================================================

        [Authorize(Roles = "Teacher")]
        [HttpGet("my-laboratories")]
        public async Task<IActionResult>
            GetMyLaboratories()
        {
            if (!TryGetCurrentUserId(
                    out int teacherUserId))
            {
                return Unauthorized();
            }

            await EnsureAuthorizationTableAsync();

            var laboratories =
                await _context
                    .TeacherLaboratoryAuthorizations
                    .AsNoTracking()
                    .Where(a =>
                        a.TeacherUserId ==
                        teacherUserId)
                    .Include(a => a.Laboratory)
                    .OrderBy(a =>
                        a.Laboratory!.LabName)
                    .Select(a => new
                    {
                        laboratoryId =
                            a.LaboratoryId,

                        labName =
                            a.Laboratory!.LabName,

                        description =
                            a.Laboratory.Description
                    })
                    .ToListAsync();

            return Ok(laboratories);
        }

        // ==========================================================
        // GET MY AUTHORIZED PCS
        // ==========================================================

        [Authorize(Roles = "Teacher")]
        [HttpGet("my-pcs")]
        public async Task<IActionResult>
            GetMyAuthorizedPCs()
        {
            if (!TryGetCurrentUserId(
                    out int teacherUserId))
            {
                return Unauthorized();
            }

            await EnsureAuthorizationTableAsync();

            var authorizedLaboratoryIds =
                _context
                    .TeacherLaboratoryAuthorizations
                    .Where(a =>
                        a.TeacherUserId ==
                        teacherUserId)
                    .Select(a =>
                        a.LaboratoryId);

            var pcs =
                await _context.PCs
                    .AsNoTracking()
                    .Include(p => p.CurrentUser)
                    .Include(p => p.Laboratory)
                    .Where(p =>
                        p.LaboratoryId.HasValue &&
                        authorizedLaboratoryIds
                            .Contains(
                                p.LaboratoryId.Value))
                    .OrderBy(p =>
                        p.PCNumber)
                    .Select(p => new
                    {
                        pcId =
                            p.PCId,

                        pcNumber =
                            p.PCNumber,

                        status =
                            p.Status,

                        currentUserId =
                            p.CurrentUserId,

                        username =
                            p.CurrentUser != null
                                ? p.CurrentUser.Username
                                : null,

                        laboratoryId =
                            p.LaboratoryId,

                        laboratoryName =
                            p.Laboratory != null
                                ? p.Laboratory.LabName
                                : null,

                        lastSeen =
                            p.LastSeen,

                        isEnabled =
                            p.IsEnabled
                    })
                    .ToListAsync();

            return Ok(pcs);
        }

        // ==========================================================
        // ADMIN - ASSIGN COMLAB TO TEACHER
        // ==========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost(
            "teacher/{teacherUserId}/laboratory/{laboratoryId}")]
        public async Task<IActionResult>
            AssignLaboratory(
                int teacherUserId,
                int laboratoryId)
        {
            await EnsureAuthorizationTableAsync();

            var teacher =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId ==
                            teacherUserId);

            if (teacher == null)
            {
                return NotFound(new
                {
                    message =
                        "Teacher account not found."
                });
            }

            if (!teacher.Role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "The selected user is not a Teacher."
                });
            }

            var laboratory =
                await _context.Laboratories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        l =>
                            l.LaboratoryId ==
                            laboratoryId);

            if (laboratory == null)
            {
                return NotFound(new
                {
                    message =
                        "Laboratory not found."
                });
            }

            bool exists =
                await _context
                    .TeacherLaboratoryAuthorizations
                    .AnyAsync(a =>
                        a.TeacherUserId ==
                        teacherUserId &&
                        a.LaboratoryId ==
                        laboratoryId);

            if (exists)
            {
                return Conflict(new
                {
                    message =
                        "Teacher is already authorized for this COMLAB."
                });
            }

            var authorization =
                new TeacherLaboratoryAuthorization
                {
                    TeacherUserId =
                        teacherUserId,

                    LaboratoryId =
                        laboratoryId,

                    CreatedAt =
                        DateTime.Now
                };

            _context
                .TeacherLaboratoryAuthorizations
                .Add(authorization);

            await _context.SaveChangesAsync();

            await LogActivityAsync(
                null,
                null,
                "Teacher COMLAB Authorized",
                $"Teacher {teacher.Username} was authorized for " +
                $"COMLAB {laboratory.LabName}."
            );

            return Ok(new
            {
                message =
                    "Teacher COMLAB authorization created.",
                teacherUserId,
                teacherUsername =
                    teacher.Username,
                laboratoryId,
                laboratoryName =
                    laboratory.LabName
            });
        }

        // ==========================================================
        // ADMIN - REVOKE COMLAB FROM TEACHER
        // ==========================================================

        [Authorize(Roles = "Admin")]
        [HttpDelete(
            "teacher/{teacherUserId}/laboratory/{laboratoryId}")]
        public async Task<IActionResult>
            RevokeLaboratory(
                int teacherUserId,
                int laboratoryId)
        {
            await EnsureAuthorizationTableAsync();

            var authorization =
                await _context
                    .TeacherLaboratoryAuthorizations
                    .Include(a =>
                        a.TeacherUser)
                    .Include(a =>
                        a.Laboratory)
                    .FirstOrDefaultAsync(a =>
                        a.TeacherUserId ==
                        teacherUserId &&
                        a.LaboratoryId ==
                        laboratoryId);

            if (authorization == null)
            {
                return NotFound(new
                {
                    message =
                        "Teacher COMLAB authorization not found."
                });
            }

            string teacherUsername =
                authorization.TeacherUser?.Username
                ?? $"User {teacherUserId}";

            string laboratoryName =
                authorization.Laboratory?.LabName
                ?? $"Laboratory {laboratoryId}";

            _context
                .TeacherLaboratoryAuthorizations
                .Remove(authorization);

            await _context.SaveChangesAsync();

            await LogActivityAsync(
                null,
                null,
                "Teacher COMLAB Authorization Revoked",
                $"Teacher {teacherUsername} was removed from " +
                $"COMLAB {laboratoryName}."
            );

            return Ok(new
            {
                message =
                    "Teacher COMLAB authorization revoked.",
                teacherUserId,
                laboratoryId
            });
        }

        // ==========================================================
        // ADMIN - GET TEACHER AUTHORIZED COMLABS
        // ==========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet("teacher/{teacherUserId}")]
        public async Task<IActionResult>
            GetTeacherAuthorizations(
                int teacherUserId)
        {
            await EnsureAuthorizationTableAsync();

            var teacher =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId ==
                            teacherUserId);

            if (teacher == null)
            {
                return NotFound(new
                {
                    message =
                        "Teacher account not found."
                });
            }

            if (!teacher.Role.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "The selected user is not a Teacher."
                });
            }

            var laboratories =
                await _context
                    .TeacherLaboratoryAuthorizations
                    .AsNoTracking()
                    .Where(a =>
                        a.TeacherUserId ==
                        teacherUserId)
                    .Include(a =>
                        a.Laboratory)
                    .OrderBy(a =>
                        a.Laboratory!.LabName)
                    .Select(a => new
                    {
                        laboratoryId =
                            a.LaboratoryId,

                        labName =
                            a.Laboratory!.LabName,

                        assignedAt =
                            a.CreatedAt
                    })
                    .ToListAsync();

            return Ok(new
            {
                teacherUserId,
                teacherUsername =
                    teacher.Username,
                laboratories
            });
        }

        // ==========================================================
        // INTERNAL - CURRENT USER ID
        // ==========================================================

        private bool TryGetCurrentUserId(
            out int userId)
        {
            string? claim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            return int.TryParse(
                claim,
                out userId);
        }

        // ==========================================================
        // INTERNAL - CREATE TABLE IF NEEDED
        // ==========================================================

        private async Task EnsureAuthorizationTableAsync()
        {
            await _context.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(
                    N'dbo.TeacherLaboratoryAuthorizations',
                    N'U'
                ) IS NULL
                BEGIN
                    CREATE TABLE dbo.TeacherLaboratoryAuthorizations
                    (
                        TeacherLaboratoryAuthorizationId INT IDENTITY(1,1)
                            NOT NULL
                            CONSTRAINT PK_TeacherLaboratoryAuthorizations
                            PRIMARY KEY,

                        TeacherUserId INT NOT NULL,

                        LaboratoryId INT NOT NULL,

                        CreatedAt DATETIME2 NOT NULL
                            CONSTRAINT DF_TeacherLaboratoryAuthorizations_CreatedAt
                            DEFAULT(GETDATE()),

                        CONSTRAINT FK_TeacherLaboratoryAuthorizations_User
                            FOREIGN KEY (TeacherUserId)
                            REFERENCES dbo.Users(UserId),

                        CONSTRAINT FK_TeacherLaboratoryAuthorizations_Laboratory
                            FOREIGN KEY (LaboratoryId)
                            REFERENCES dbo.Laboratories(LaboratoryId),

                        CONSTRAINT UQ_TeacherLaboratoryAuthorizations
                            UNIQUE(TeacherUserId, LaboratoryId)
                    );
                END
                """);
        }

        // ==========================================================
        // ACTIVITY LOG
        // ==========================================================

        private async Task LogActivityAsync(
            int? userId,
            int? pcId,
            string action,
            string details)
        {
            var log =
                new ActivityLog
                {
                    UserId =
                        userId,

                    PCId =
                        pcId,

                    Action =
                        action,

                    Details =
                        details,

                    CreatedAt =
                        DateTime.Now
                };

            _context.ActivityLogs.Add(log);

            await _context.SaveChangesAsync();
        }
    }
}
