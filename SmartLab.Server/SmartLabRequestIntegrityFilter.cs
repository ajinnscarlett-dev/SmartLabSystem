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

            // Authentication state is backed by the database as well as the JWT.
            // This immediately rejects tokens belonging to deleted/deactivated users
            // and forces a fresh login after an administrator changes the account role.
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

            // New/admin-provisioned and administrator-reset passwords must be changed
            // before the account can perform any operational action.
            if (!string.Equals(controller, "AuthController", StringComparison.OrdinalIgnoreCase) &&
                currentUser.MustChangePassword)
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

                    bool authorized = await _context.TeacherLaboratoryAuthorizations.AsNoTracking()
                        .AnyAsync(a => a.TeacherUserId == currentUserId && a.LaboratoryId == laboratoryId);
                    if (!authorized)
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

                    bool authorized = await _context.PCs.AsNoTracking().AnyAsync(pc =>
                        pc.PCId == pcId && pc.LaboratoryId.HasValue &&
                        _context.TeacherLaboratoryAuthorizations.Any(a =>
                            a.TeacherUserId == currentUserId && a.LaboratoryId == pc.LaboratoryId.Value));
                    if (!authorized)
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

                    PC? ownedPc = await _context.PCs
                        .FirstOrDefaultAsync(p => p.PCId == pcId && p.CurrentUserId == currentUserId);

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

            // Preserve the account-wide password policy even for the older Admin user
            // controller, which is still retained for client compatibility. The
            // canonical management controller already sets this flag itself.
            if (controller == "UserController" &&
                action.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase) &&
                context.Result is ObjectResult result &&
                (result.StatusCode == null || (result.StatusCode >= 200 && result.StatusCode < 300)) &&
                TryGetRouteInt(context, "id", out int resetUserId))
            {
                User? resetUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == resetUserId);
                if (resetUser != null && !resetUser.MustChangePassword)
                {
                    resetUser.MustChangePassword = true;
                    _context.Entry(resetUser).Property(u => u.MustChangePassword).IsModified = true;
                    await _context.SaveChangesAsync();
                }
            }
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
            value = 0;
            return context.RouteData.Values.TryGetValue(name, out object? routeValue) && int.TryParse(routeValue?.ToString(), out value);
        }
    }
}
