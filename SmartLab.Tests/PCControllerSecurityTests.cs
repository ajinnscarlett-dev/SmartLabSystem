using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using SmartLab.Server.Controllers;
using Xunit;

namespace SmartLab.Tests;

public sealed class PCControllerSecurityTests
{
    [Fact]
    public async Task StudentLoginRejectsUnregisteredPcNumber()
    {
        await using AppDbContext context = CreateContext();
        context.Users.Add(new User
        {
            UserId = 1,
            Username = "student01",
            PasswordHash = "hash",
            Role = "Student",
            MustChangePassword = false
        });
        await context.SaveChangesAsync();

        PCController controller = CreateController(context, 1, "Student");

        IActionResult result = await controller.LoginToPC(
            "SCARLET-999",
            1,
            new PCLoginRequest { MACAddress = "AA:BB:CC:DD:EE:FF" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task StudentLoginRejectsWrongRegisteredMac()
    {
        await using AppDbContext context = CreateContext();
        context.Users.Add(new User
        {
            UserId = 1,
            Username = "student01",
            PasswordHash = "hash",
            Role = "Student",
            MustChangePassword = false
        });
        context.PCs.Add(new PC
        {
            PCId = 10,
            PCNumber = "SCARLET-01",
            Status = "Available",
            MACAddress = "AABBCCDDEEFF",
            IsEnabled = true
        });
        await context.SaveChangesAsync();

        PCController controller = CreateController(context, 1, "Student");

        IActionResult result = await controller.LoginToPC(
            "SCARLET-01",
            1,
            new PCLoginRequest { MACAddress = "11:22:33:44:55:66" });

        ObjectResult response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StudentCannotReleaseAnotherStudentsPc()
    {
        await using AppDbContext context = CreateContext();
        context.Users.AddRange(
            new User { UserId = 1, Username = "student01", PasswordHash = "hash", Role = "Student", MustChangePassword = false },
            new User { UserId = 2, Username = "student02", PasswordHash = "hash", Role = "Student", MustChangePassword = false });
        context.PCs.Add(new PC
        {
            PCId = 10,
            PCNumber = "SCARLET-01",
            Status = "Occupied",
            CurrentUserId = 2,
            MACAddress = "AABBCCDDEEFF",
            IsEnabled = true
        });
        await context.SaveChangesAsync();

        PCController controller = CreateController(context, 1, "Student");

        IActionResult result = await controller.ReleasePC(1);

        Assert.IsType<NotFoundObjectResult>(result);
        PC pc = await context.PCs.SingleAsync(p => p.PCId == 10);
        Assert.Equal(2, pc.CurrentUserId);
        Assert.Equal("Occupied", pc.Status);
    }

    [Fact]
    public async Task StudentLoginRejectsOccupiedPc()
    {
        await using AppDbContext context = CreateContext();
        context.Users.AddRange(
            new User { UserId = 1, Username = "student01", PasswordHash = "hash", Role = "Student", MustChangePassword = false },
            new User { UserId = 2, Username = "student02", PasswordHash = "hash", Role = "Student", MustChangePassword = false });
        context.PCs.Add(new PC
        {
            PCId = 10,
            PCNumber = "SCARLET-01",
            Status = "Occupied",
            CurrentUserId = 2,
            MACAddress = "AABBCCDDEEFF",
            IsEnabled = true
        });
        await context.SaveChangesAsync();

        PCController controller = CreateController(context, 1, "Student");

        IActionResult result = await controller.LoginToPC(
            "SCARLET-01",
            1,
            new PCLoginRequest { MACAddress = "AA-BB-CC-DD-EE-FF" });

        Assert.IsType<ConflictObjectResult>(result);
        PC pc = await context.PCs.SingleAsync(p => p.PCId == 10);
        Assert.Equal(2, pc.CurrentUserId);
        Assert.Equal("Occupied", pc.Status);
    }

    private static PCController CreateController(AppDbContext context, int userId, string role)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            },
            "unit-test");

        return new PCController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
