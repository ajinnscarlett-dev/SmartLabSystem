using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SmartLab.Server
{
    public sealed class SmartLabAuthorizationFilter : IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata
                .OfType<AllowAnonymousAttribute>()
                .Any())
            {
                return Task.CompletedTask;
            }

            // Defense-in-depth for every controller/action, including endpoints
            // that accidentally omit [Authorize]. Program.cs also has a fallback
            // authorization policy for the same closed-LAN boundary.
            if (!(context.HttpContext.User?.Identity?.IsAuthenticated ?? false))
            {
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            string controller = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName)
                ? controllerName ?? string.Empty
                : string.Empty;
            string action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName)
                ? actionName ?? string.Empty
                : string.Empty;

            if (controller == "PC")
            {
                AuthorizePcController(context, action);
            }
            else if (controller == "ServiceDesk")
            {
                AuthorizeServiceDeskController(context, action);
            }

            return Task.CompletedTask;
        }

        private static void AuthorizePcController(
            AuthorizationFilterContext context,
            string action)
        {
            if (action.Equals("GetAllPCs", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GetPCsByLaboratory", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GetPC", StringComparison.OrdinalIgnoreCase))
            {
                RequireAnyRole(context, "Admin", "Teacher");
                return;
            }

            // PC ownership is a student operation. Teacher/Admin workstation
            // actions go through PCCommandController and its existing lab checks.
            if (action.Equals("LoginToPC", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("ReleasePC", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                RequireRole(context, "Student");
                return;
            }

            if (action.Equals("AssignPC", StringComparison.OrdinalIgnoreCase))
            {
                RequireRole(context, "Admin");
                return;
            }

            // PC inventory/configuration/maintenance endpoints are Admin/MIS operations.
            RequireRole(context, "Admin");
        }

        private static void AuthorizeServiceDeskController(
            AuthorizationFilterContext context,
            string action)
        {
            if (action.Equals("CreateTicket", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GetTeacherTickets", StringComparison.OrdinalIgnoreCase))
            {
                RequireRole(context, "Teacher");

                if (context.Result != null)
                {
                    return;
                }

                if (context.RouteData.Values.TryGetValue("teacherUserId", out object? routeValue) &&
                    int.TryParse(routeValue?.ToString(), out int teacherUserId) &&
                    int.TryParse(
                        context.HttpContext.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
                        out int authenticatedUserId) &&
                    teacherUserId != authenticatedUserId)
                {
                    context.Result = new ForbidResult();
                }

                return;
            }

            RequireRole(context, "Admin");
        }

        private static void RequireAnyRole(
            AuthorizationFilterContext context,
            params string[] roles)
        {
            if (!roles.Any(context.HttpContext.User.IsInRole))
            {
                context.Result = new ForbidResult();
            }
        }

        private static void RequireRole(
            AuthorizationFilterContext context,
            string role)
        {
            if (!context.HttpContext.User.IsInRole(role))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
