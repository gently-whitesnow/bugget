using System.Security.Claims;
using Authorization.Api.Controllers;
using Authorization.Api.Interfaces;
using Authorization.Api.Models;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Authorization.Tests;

public sealed class FlagsControllerTests
{
    [Fact]
    public async Task Get_ReturnsBetaTestTrue_ForAdmin()
    {
        const long userId = 42L;
        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        usersService.Setup(s => s.IsAdminAsync(userId)).ReturnsAsync(true);

        var hostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var adminAccess = new AdminAccessService(
            usersService.Object,
            hostEnvironment.Object,
            Mock.Of<ILogger<AdminAccessService>>());

        var controller = CreateController(adminAccess, userId);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var view = Assert.IsType<FlagsView>(ok.Value);
        Assert.True(view.BetaTest);
    }

    [Fact]
    public async Task Get_ReturnsBetaTestFalse_ForNonAdmin()
    {
        const long userId = 42L;
        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        usersService.Setup(s => s.IsAdminAsync(userId)).ReturnsAsync(false);

        var hostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var adminAccess = new AdminAccessService(
            usersService.Object,
            hostEnvironment.Object,
            Mock.Of<ILogger<AdminAccessService>>());

        var controller = CreateController(adminAccess, userId);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var view = Assert.IsType<FlagsView>(ok.Value);
        Assert.False(view.BetaTest);
    }

    private static FlagsController CreateController(AdminAccessService adminAccess, long userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new FlagsController(adminAccess)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }
}
