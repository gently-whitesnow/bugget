using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Authorization.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OidcAuth;
using OidcAuth.Models;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Authorization.Tests.OidcAuth;

public class OidcControllerTests
{
    [Fact]
    public async Task Callback_ValidTokenInCookie_AuthorizesAndRedirects()
    {
        // Arrange
        const string externalId = "oidc-user-123";
        const string nextPath = "/dashboard";

        var tokenValidator = CreateMockTokenValidator(externalId);
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator, externalAuth.Object);

        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=valid.jwt.token";
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?next={nextPath}");

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.EndsWith(nextPath, redirect.Url);

        externalAuth.Verify(e => e.AuthorizeAsync(
            It.IsAny<HttpContext>(),
            It.Is<IExternalUser>(u => u.ExternalId == externalId),
            true,
            "oidc",
            null),
            Times.Once);
    }

    [Fact]
    public async Task Callback_ValidTokenInHeader_AuthorizesAndRedirects()
    {
        // Arrange
        const string externalId = "oidc-user-header";
        const string nextPath = "/app";

        var tokenValidator = CreateMockTokenValidator(externalId);
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator, externalAuth.Object, tokenHeaderName: "X-Id-Token");

        // Token in header (with Bearer prefix)
        controller.ControllerContext.HttpContext.Request.Headers["X-Id-Token"] = "Bearer valid.jwt.token";
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?next={nextPath}");

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.EndsWith(nextPath, redirect.Url);

        externalAuth.Verify(e => e.AuthorizeAsync(
            It.IsAny<HttpContext>(),
            It.Is<IExternalUser>(u => u.ExternalId == externalId),
            true,
            "oidc",
            null),
            Times.Once);
    }

    [Fact]
    public async Task Callback_ValidTokenInHeaderWithoutBearerPrefix_AuthorizesAndRedirects()
    {
        // Arrange
        const string externalId = "oidc-user-raw";

        var tokenValidator = CreateMockTokenValidator(externalId);
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator, externalAuth.Object, tokenHeaderName: "X-Id-Token");

        // Token in header without Bearer prefix
        controller.ControllerContext.HttpContext.Request.Headers["X-Id-Token"] = "valid.jwt.token";
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?next=/");

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        Assert.IsType<RedirectResult>(result);
        externalAuth.Verify(
            e => e.AuthorizeAsync(It.IsAny<HttpContext>(), It.IsAny<IExternalUser>(), true, "oidc", null),
            Times.Once);
    }

    [Fact]
    public async Task Callback_HeaderPriorityOverCookie()
    {
        // Arrange - header user should win over cookie user
        const string headerExternalId = "header-user";
        const string cookieExternalId = "cookie-user";

        var tokenValidator = new Mock<IOidcTokenValidator>();

        // Setup to return different users based on token value
        var headerPrincipal = CreatePrincipal(headerExternalId);
        var cookiePrincipal = CreatePrincipal(cookieExternalId);

        tokenValidator.Setup(v => v.ValidateTokenAsync("header-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(headerPrincipal);
        tokenValidator.Setup(v => v.ValidateTokenAsync("cookie-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cookiePrincipal);
        tokenValidator.Setup(v => v.GetSubject(headerPrincipal)).Returns(headerExternalId);
        tokenValidator.Setup(v => v.GetSubject(cookiePrincipal)).Returns(cookieExternalId);

        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator.Object, externalAuth.Object, tokenHeaderName: "X-Id-Token");

        // Both header and cookie set
        controller.ControllerContext.HttpContext.Request.Headers["X-Id-Token"] = "header-token";
        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=cookie-token";
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?next=/");

        // Act
        var result = await controller.CallbackAsync();

        // Assert - should use header token, not cookie
        externalAuth.Verify(e => e.AuthorizeAsync(
            It.IsAny<HttpContext>(),
            It.Is<IExternalUser>(u => u.ExternalId == headerExternalId),
            true,
            "oidc",
            null),
            Times.Once);
    }

    [Fact]
    public async Task Callback_NoToken_ReturnsUnauthorized()
    {
        // Arrange
        var tokenValidator = new Mock<IOidcTokenValidator>();
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator.Object, externalAuth.Object);

        // No cookie or header set

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        // Тело собирает общий адаптер границы, а не контроллер: причина остаётся в журнале.
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var tokenValidator = new Mock<IOidcTokenValidator>();
        tokenValidator.Setup(v => v.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal?)null);

        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator.Object, externalAuth.Object);

        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=invalid.token";

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        // Тело собирает общий адаптер границы, а не контроллер: причина остаётся в журнале.
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_NoSubjectClaim_ReturnsUnauthorized()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var tokenValidator = new Mock<IOidcTokenValidator>();
        tokenValidator.Setup(v => v.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principal);
        tokenValidator.Setup(v => v.GetSubject(principal))
            .Returns((string?)null);

        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator.Object, externalAuth.Object);

        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=token.without.sub";

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        // Тело собирает общий адаптер границы, а не контроллер: причина остаётся в журнале.
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_NoNextParam_RedirectsToRoot()
    {
        // Arrange
        const string externalId = "oidc-user-456";

        var tokenValidator = CreateMockTokenValidator(externalId);
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator, externalAuth.Object);

        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=valid.jwt.token";
        // No ?next query param

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.EndsWith("/", redirect.Url);
    }

    [Fact]
    public async Task Callback_MaliciousNext_SanitizesToRoot()
    {
        // Arrange
        const string externalId = "oidc-user-789";

        var tokenValidator = CreateMockTokenValidator(externalId);
        var externalAuth = new Mock<IExternalAuthService>();
        var controller = CreateController(tokenValidator, externalAuth.Object);

        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] = "_oauth2_proxy=valid.jwt.token";
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?next=https://evil.com/steal");

        // Act
        var result = await controller.CallbackAsync();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.EndsWith("/", redirect.Url);
    }

    private static ClaimsPrincipal CreatePrincipal(string externalId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, externalId),
            new Claim("sub", externalId)
        }, "test"));
    }

    private static IOidcTokenValidator CreateMockTokenValidator(string externalId)
    {
        var principal = CreatePrincipal(externalId);

        var mock = new Mock<IOidcTokenValidator>();
        mock.Setup(v => v.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principal);
        mock.Setup(v => v.GetSubject(principal))
            .Returns(externalId);

        return mock.Object;
    }

    private static OidcController CreateController(
        IOidcTokenValidator tokenValidator,
        IExternalAuthService externalAuth,
        string? tokenHeaderName = null)
    {
        Environment.SetEnvironmentVariable("APP_DOMAIN", "https://test.example.com");

        var oidcOptions = MsOptions.Create(new OidcAuthOptions
        {
            Authority = "https://example.com/realms/test",
            CookieName = "_oauth2_proxy",
            TokenHeaderName = tokenHeaderName
        });

        var controller = new OidcController(
            tokenValidator,
            externalAuth,
            oidcOptions,
            NullLogger<OidcController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }
}
