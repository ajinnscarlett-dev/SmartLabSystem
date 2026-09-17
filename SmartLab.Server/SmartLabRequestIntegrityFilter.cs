using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SmartLab.Server.Controllers;

namespace SmartLab.Server
{
    public sealed class SmartLabRequestIntegrityFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;
        private readonly TeacherScheduleService _scheduleService;

        public SmartLabRequestIntegrityFilter(AppDbContext context, TeacherScheduleService scheduleService)
        {
            _context = context;
            _scheduleService = scheduleService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!(context.HttpContext.User?.Identity?.IsAuthenticated ?? false))
            {
                await next();
                return;
            }

            string controller = context.Controller.GetType().Name;
            string action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName)
                ? actionName ?? string.Empty
                : string.Empty;

            if (!TryGetUserId(context, out int currentUserId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            User? currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == currentUserId);

            if (currentUser == null || currentUser.Role.StartsWith("Disabled:", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    message = "This SmartLab account is no longer active."
                });
                return;
            }

            string tokenRole = context.HttpContext.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            if (!string.Equals(currentUser.Role, tokenRole, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    message = "Your SmartLab access has changed. Please sign in again."
                });
                return;
            }

            if (!string.Equals(controller, "AuthController", StringComparison.OrdinalIgnoreCase) && currentUser.MustChangePassword)
            {
                context.Result = new ObjectResult(new
                {
                    code = "PASSWORD_CHANGE_REQUIRED",
                    message = "You must change your password before continuing."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            if (controller == "PCController" && context.HttpContext.User.IsInRole("Teacher"))
            {
                if (action.Equals("GetAllPCs", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new OkObjectResult(await GetAuthorizedTeacherPcsAsync(currentUserId));
                    return;
                }

                if (action.Equals("GetPCsByLaboratory", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "laboratoryId", out int laboratoryId))
                    {
                        context.Result = new BadRequestResult();
                        return;
                    }

                    if (!await _scheduleService.IsTeacherScheduledAsync(currentUserId, laboratoryId))
                    {
                        context.Result = new ForbidResult();
                        return;
                    }

                    context.Result = new OkObjectResult(await GetAuthorizedTeacherPcsAsync(currentUserId, laboratoryId));
                    return;
                }

                if (action.Equals("GetPC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "id", out int pcId))
                    {
                        context.Result = new BadRequestResult();
                        return;
                    }

                    PC? pc = await _context.PCs.AsNoTracking().FirstOrDefaultAsync(p => p.PCId == pcId);
                    if (pc == null || !pc.LaboratoryId.HasValue ||
                        !await _scheduleService.IsTeacherScheduledAsync(currentUserId, pc.LaboratoryId.Value))
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }

            if (controller == "PCController" && context.HttpContext.User.IsInRole("Student"))
            {
                if (action.Equals("LoginToPC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "userId", out int routeUserId) || routeUserId != currentUserId)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
                else if (action.Equals("ReleasePC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "userId", out int routeUserId) || routeUserId != currentUserId)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
                else if (action.Equals("Heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "id", out int pcId))
                    {
                        context.Result = new BadRequestResult();
                        return;
                    }

                    PC? ownedPc = await _context.PCs.FirstOrDefaultAsync(p => p.PCId == pcId && p.CurrentUserId == currentUserId);
                    if (ownedPc == null)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }

                    if (string.Equals(ownedPc.Status, "Offline", StringComparison.OrdinalIgnoreCase))
                    {
                        ownedPc.Status = "Occupied";
                        await _context.SaveChangesAsync();
                    }
                }
            }

            if (controller == "ServiceDeskController" && action.Equals("CreateTicket", StringComparison.OrdinalIgnoreCase))
            {
                if (!context.HttpContext.User.IsInRole("Teacher"))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                if (!context.ActionArguments.TryGetValue("request", out object? request) || request is not ServiceDeskCreateRequest createRequest)
                {
                    context.Result = new BadRequestObjectResult(new { message = "Invalid Service Desk request." });
                    return;
                }

                if (createRequest.TeacherUserId != currentUserId)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                string authenticatedUsername = context.HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
                if (!string.Equals(createRequest.TeacherUsername?.Trim(), authenticatedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            await next();
        }

        private async Task<List<object>> GetAuthorizedTeacherPcsAsync(int teacherUserId, int? laboratoryId = null)
        {
            DateTime now = DateTime.Now;
            DateTime dayStart = now.Date;
            DateTime dayEnd = dayStart.AddDays(1);
            TimeSpan time = now.TimeOfDay;

            return await _context.PCs.AsNoTracking()
                .Where(p => p.LaboratoryId.HasValue &&
                    (!laboratoryId.HasValue || p.LaboratoryId == laboratoryId.Value) &&
                    _context.ClassSchedules.Any(s =>
                        s.TeacherUserId == teacherUserId &&
                        s.LaboratoryId == p.LaboratoryId.Value &&
                        s.ScheduleDate >= dayStart &&
                        s.ScheduleDate < dayEnd &&
                        s.Status == "Scheduled" &&
                        s.StartTime <= time &&
                        s.EndTime > time))
                .Include(p => p.CurrentUser)
                .Include(p => p.Laboratory)
                .OrderBy(p => p.PCId)
                .Select(p => (object)new
                {
                    p.PCId,
                    p.PCNumber,
                    p.Status,
                    p.CurrentUserId,
                    Username = p.CurrentUser != null ? p.CurrentUser.Username : null,
                    p.LaboratoryId,
                    LaboratoryName = p.Laboratory != null ? p.Laboratory.LabName : null,
                    p.MACAddress,
                    p.IPAddress,
                    p.LastSeen,
                    p.IsEnabled,
                    p.MaintenanceReason,
                    p.MaintenanceStarted
                })
                .ToListAsync();
        }

        private static bool TryGetUserId(ActionContext context, out int userId) =>
            int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private static bool TryGetRouteInt(ActionContext context, string name, out int value)
        {
            value = 0;
            return context.RouteData.Values.TryGetValue(name, out object? routeValue) && int.TryParse(routeValue?.ToString(), out value);
        }
    }
}
