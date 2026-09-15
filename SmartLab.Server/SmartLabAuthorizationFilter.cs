using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace SmartLab.Server
{
    public sealed class SmartLabAuthorizationFilter : IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata
                .OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
                .Any())
            {
                return Task.CompletedTask;
            }

            string controller = context.Controller.GetType().Name;
            string action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName)
                ? actionName ?? string.Empty
                : string.Empty;

            if (controller == "PCController")
            {
                AuthorizePcController(context, action);
            }
            else if (controller == "ServiceDeskController")
            {
                AuthorizeServiceDeskController(context, action);
            }

            return Task.CompletedTask;
        }

        private static void AuthorizePcController(
            AuthorizationFilterContext context,
            string action)
        {
            if (!(context.HttpContext.User?.Identity?.IsAuthenticated ?? false))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (action.Equals("GetAllPCs", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GetPCsByLaboratory", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GetPC", StringComparison.OrdinalIgnoreCase))
            {
                RequireAnyRole(context, "Admin", "Teacher");
                return;
            }

            if (action.Equals("LoginToPC", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("ReleasePC", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                RequireAnyRole(context, "Student", "Teacher", "Admin");
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
            if (!(context.HttpContext.User?.Identity?.IsAuthenticated ?? false))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

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
                        context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
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
