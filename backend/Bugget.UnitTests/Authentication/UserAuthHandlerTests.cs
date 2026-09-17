using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugget.Api.Authentication;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Domain;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Bugget.UnitTests.Authentication;

public class UserAuthHandlerTests
{
    private static UserAuthHandler CreateHandler(
        DefaultHttpContext context,
        AuthHeadersOptions headersOptions)
    {
        var authSchemeOptions = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        authSchemeOptions
            .Setup(o => o.Get(It.IsAny<string?>()))
            .Returns(new AuthenticationSchemeOptions());

        var headersOptionsMonitor = new Mock<IOptionsMonitor<AuthHeadersOptions>>();
        headersOptionsMonitor
            .Setup(o => o.CurrentValue)
            .Returns(headersOptions);

        var handler = new UserAuthHandler(
            authSchemeOptions.Object,
            LoggerFactory.Create(_ => { }),
            UrlEncoder.Default,
            headersOptionsMonitor.Object);

        handler.InitializeAsync(
            new AuthenticationScheme("headers", null, typeof(UserAuthHandler)),
            context).Wait();
        return handler;
    }

    [Fact]
    public async Task Succeeds_WhenAllHeadersAreValid()
    {
        var headersOptions = new AuthHeadersOptions
        {
            UserIdHeaderName = "X-User-Id",
            TeamIdHeaderName = "X-Team-Id",
            OrganizationIdHeaderName = "X-Org-Id",
            AuthMethodHeaderName = "Auth-Request-Auth-Method"
        };
        var context = new DefaultHttpContext();
        context.Request.Headers[headersOptions.UserIdHeaderName] = "user-123";
        context.Request.Headers[headersOptions.TeamIdHeaderName] = "team-456";
        context.Request.Headers[headersOptions.OrganizationIdHeaderName] = "org-789";
        context.Request.Headers[headersOptions.AuthMethodHeaderName] = AuthMethods.Pat;
        context.Request.Headers["X-Signal-R-Connection-Id"] = "conn-abc";

        var usersClient = new Mock<IUsersClient>();
        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var claims = result.Principal!.Claims.ToDictionary(c => c.Type, c => c.Value);
        Assert.Equal("user-123", claims[ClaimTypes.NameIdentifier]);
        Assert.Equal("team-456", claims["team_id"]);
        Assert.Equal("org-789", claims["organization_id"]);
        Assert.Equal("conn-abc", claims["signalr_connection_id"]);
        Assert.Equal(AuthMethods.Pat, claims[AuthClaims.AuthMethod]);
    }

    [Fact(DisplayName =
        "Когда имя заголовка способа входа не задано в конфигурации, а запрос принёс отметку PAT — " +
        "claim способа входа не создаётся и автор действий определяется как CreatorType равен 0 - \"Пользователь\", " +
        "а не 3 - \"Неинтерактивный клиент: запрос пришёл через PAT, а не браузерную JWT-сессию.\"")]
    public async Task AuthenticateAsync_ShouldDegradeActorToUser_WhenAuthMethodHeaderNameIsNotConfigured()
    {
        // Конфигурация без AuthMethodHeaderName — форма прод-инцидента (bugget report 436, баг 2): nginx принёс
        // Auth-Request-Auth-Method: pat, но хендлер не знает имени заголовка и молча теряет способ входа.
        var headersOptions = new AuthHeadersOptions
        {
            UserIdHeaderName = "X-User-Id"
        };
        var context = new DefaultHttpContext();
        context.Request.Headers[headersOptions.UserIdHeaderName] = "user-123";
        context.Request.Headers["Auth-Request-Auth-Method"] = AuthMethods.Pat;

        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        // Аутентификация успешна, но claim нет — атрибуция деградирует к User
        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(AuthClaims.AuthMethod));
        Assert.Equal(Bugget.Domain.Common.CreatorType.User, result.Principal.GetIdentity().ActorCreatorType);
    }

    [Fact]
    public async Task Fails_WhenUserHeaderConfiguredButMissing()
    {
        // only user header configured
        var headersOptions = new AuthHeadersOptions
        {
            UserIdHeaderName = "X-User-Id"
        };
        var context = new DefaultHttpContext(); // no headers

        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("User ID not found", result.Failure?.Message);
    }

    [Fact]
    public async Task Succeeds_WhenUserHeaderMissingButNotConfigured()
    {
        // no headers configured
        var headersOptions = new AuthHeadersOptions();
        var context = new DefaultHttpContext();

        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("default-user", result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task Fails_WhenTeamHeaderConfiguredButEmpty()
    {
        var headersOptions = new AuthHeadersOptions
        {
            UserIdHeaderName = "X-User-Id",
            TeamIdHeaderName = "X-Team-Id"
        };
        var context = new DefaultHttpContext();
        context.Request.Headers[headersOptions.UserIdHeaderName] = "user-123";
        context.Request.Headers[headersOptions.TeamIdHeaderName] = string.Empty;

        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Team ID not found", result.Failure?.Message);
    }

    [Fact]
    public async Task Fails_WhenOrgHeaderConfiguredButEmpty()
    {
        var headersOptions = new AuthHeadersOptions
        {
            UserIdHeaderName = "X-User-Id",
            OrganizationIdHeaderName = "X-Org-Id"
        };
        var context = new DefaultHttpContext();
        context.Request.Headers[headersOptions.UserIdHeaderName] = "user-123";
        context.Request.Headers[headersOptions.OrganizationIdHeaderName] = string.Empty;

        var handler = CreateHandler(context, headersOptions);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Organization ID not found", result.Failure?.Message);
    }
}
