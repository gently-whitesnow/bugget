using Authorization.Api.Interfaces;
using Authorization.Api.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Authorization.Tests;

public sealed class AdminAccessServiceTests
{
    [Fact]
    public async Task HasAccessAsync_ReturnsTrueInDevelopment()
    {
        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        var hostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        hostEnvironment.SetupGet(env => env.EnvironmentName).Returns(Environments.Development);

        var sut = new AdminAccessService(
            usersService.Object,
            hostEnvironment.Object,
            Mock.Of<ILogger<AdminAccessService>>());

        var result = await sut.HasAccessAsync(123L);

        Assert.True(result);
        usersService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HasAccessAsync_UsesUsersServiceOutsideDevelopment()
    {
        const long userId = 123L;

        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        usersService.Setup(service => service.IsAdminAsync(userId)).ReturnsAsync(true);

        var hostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        hostEnvironment.SetupGet(env => env.EnvironmentName).Returns(Environments.Production);

        var sut = new AdminAccessService(
            usersService.Object,
            hostEnvironment.Object,
            Mock.Of<ILogger<AdminAccessService>>());

        var result = await sut.HasAccessAsync(userId);

        Assert.True(result);
        usersService.Verify(service => service.IsAdminAsync(userId), Times.Once);
    }

    [Fact]
    public async Task HasAccessAsync_ReturnsFalseWhenUsersServiceFails()
    {
        const long userId = 123L;

        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        usersService
            .Setup(service => service.IsAdminAsync(userId))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var hostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        hostEnvironment.SetupGet(env => env.EnvironmentName).Returns(Environments.Production);

        var sut = new AdminAccessService(
            usersService.Object,
            hostEnvironment.Object,
            Mock.Of<ILogger<AdminAccessService>>());

        var result = await sut.HasAccessAsync(userId);

        Assert.False(result);
        usersService.Verify(service => service.IsAdminAsync(userId), Times.Once);
    }
}
