using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server.Controllers;

namespace SmartLab.Server;

// A provisioned secret supplements MAC identity; it never replaces user or lab authorization.
public sealed class WorkstationCredentialFilter(AppDbContext db, IConfiguration config, IHostEnvironment environment, ILogger<WorkstationCredentialFilter> logger)
    : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -1000;
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        bool presence = context.Controller is MachinePresenceController;
        bool student = context.HttpContext.User.IsInRole("Student");
        if ((!presence && !student) || context.Controller is AuthController) { await next(); return; }

        string? pcNumber = null;
        if (presence && context.ActionArguments.TryGetValue("request", out var body) && body is MachinePresenceRequest request)
            pcNumber = request.PCNumber?.Trim();
        else if (context.Controller is PCController && context.ActionArguments.TryGetValue("pcNumber", out var number))
            pcNumber = number?.ToString()?.Trim();
        else if (int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            pcNumber = await db.PCs.Where(p => p.CurrentUserId == userId).Select(p => p.PCNumber).SingleOrDefaultAsync();

        string? expected = string.IsNullOrWhiteSpace(pcNumber) ? null : config[$"SmartLab:DeviceTokenHashes:{pcNumber}"];
        bool required = config.GetValue("SmartLab:RequireDeviceCredentials", !environment.IsDevelopment());
        if (!required && string.IsNullOrWhiteSpace(expected)) { await next(); return; }

        string token = context.HttpContext.Request.Headers["X-SmartLab-Device-Token"].ToString();
        if (!Matches(token, expected))
        {
            logger.LogWarning("Device credential rejected for workstation {PCNumber}", pcNumber);
            context.Result = new ObjectResult(new { message = "Workstation credential missing or invalid. Contact Admin/MIS." }) { StatusCode = 403 };
            return;
        }
        await next();
    }

    public static bool Matches(string? token, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32 || token.Length > 256 || expectedHash?.Length != 64) return false;
        try { return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(token)), Convert.FromHexString(expectedHash)); }
        catch (FormatException) { return false; }
    }
}