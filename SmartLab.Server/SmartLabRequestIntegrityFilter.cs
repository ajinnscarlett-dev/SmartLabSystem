using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SmartLab.Server
{
    public sealed class SmartLabRequestIntegrityFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;

        public SmartLabRequestIntegrityFilter(AppDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
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

            if (controller == "PCController" && context.HttpContext.User.IsInRole("Teacher"))
            {
                if (!TryGetUserId(context, out int teacherUserId))
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                if (action.Equals("GetAllPCs", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new OkObjectResult(await GetAuthorizedTeacherPcsAsync(teacherUserId));
                    return;
                }

                if (action.Equals("GetPCsByLaboratory", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "laboratoryId", out int laboratoryId))
                    {
                        context.Result = new BadRequestResult();
                        return;
                    }

                    bool authorized = await _context.TeacherLaboratoryAuthorizations.AsNoTracking()
                        .AnyAsync(a => a.TeacherUserId == teacherUserId && a.LaboratoryId == laboratoryId);
                    if (!authorized)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }

                    context.Result = new OkObjectResult(await GetAuthorizedTeacherPcsAsync(teacherUserId, laboratoryId));
                    return;
                }

                if (action.Equals("GetPC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "id", out int pcId))
                    {
                        context.Result = new BadRequestResult();
                        return;
                    }

                    bool authorized = await _context.PCs.AsNoTracking().AnyAsync(pc =>
                        pc.PCId == pcId && pc.LaboratoryId.HasValue &&
                        _context.TeacherLaboratoryAuthorizations.Any(a =>
                            a.TeacherUserId == teacherUserId && a.LaboratoryId == pc.LaboratoryId.Value));
                    if (!authorized)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }

            if (controller == "PCController" && context.HttpContext.User.IsInRole("Student"))
            {
                if (!TryGetUserId(context, out int userId))
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                if (action.Equals("LoginToPC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "userId", out int routeUserId) || routeUserId != userId)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
                else if (action.Equals("ReleasePC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetRouteInt(context, "userId", out int routeUserId) || routeUserId != userId)
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

                    bool ownsPc = await _context.PCs.AsNoTracking().AnyAsync(p => p.PCId == pcId && p.CurrentUserId == userId);
                    if (!ownsPc)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }

            if (controller == "ServiceDeskController" && action.Equals("CreateTicket", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetUserId(context, out int userId) || !context.HttpContext.User.IsInRole("Teacher"))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                if (!context.ActionArguments.TryGetValue("request", out object? request) || request is not ServiceDeskCreateRequest createRequest)
                {
                    context.Result = new BadRequestObjectResult(new { message = "Invalid Service Desk request." });
                    return;
                }

                if (createRequest.TeacherUserId != userId)
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
            return await _context.PCs.AsNoTracking()
                .Where(p => p.LaboratoryId.HasValue &&
                    (!laboratoryId.HasValue || p.LaboratoryId == laboratoryId.Value) &&
                    _context.TeacherLaboratoryAuthorizations.Any(a =>
                        a.TeacherUserId == teacherUserId && a.LaboratoryId == p.LaboratoryId.Value))
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

        private static bool TryGetUserId(ActionContext context, out int userId)
        {
            return int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        private static bool TryGetRouteInt(ActionContext context, string name, out int value)
        {
            return context.RouteData.Values.TryGetValue(name, out object? routeValue) && int.TryParse(routeValue?.ToString(), out value);
        }
    }
}
