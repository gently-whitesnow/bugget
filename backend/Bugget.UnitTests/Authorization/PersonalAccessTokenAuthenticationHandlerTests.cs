using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugget.Api.Authorization;
using Bugget.Api.Authorization.Authentication;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Application.Authorization;
using Bugget.Domain.Authentication;
using Bugget.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using AuthUser = Bugget.Application.Authorization.User;

namespace Bugget.UnitTests.Authorization;

public sealed class PersonalAccessTokenAuthenticationHandlerTests
{
    private readonly Mock<IUsersClient> _usersClient = new();
    private readonly Mock<IUsersService> _users = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task NoResult_WhenAuthorizationIsNotPatFormat()
    {
        var result = await AuthenticateAsync(authorization: "Bearer eyJhbGciOiJIUzI1NiJ9.e30.sig");

        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
        _usersClient.Verify(t => t.FindPersonalAccessTokenAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Fail_WhenTokenUnknown()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        _usersClient.Setup(t => t.FindPersonalAccessTokenAsync(generated.Hash)).ReturnsAsync((PersonalAccessToken?)null);

        var result = await AuthenticateAsync(
            authorization: $"Bearer {generated.Value}",
            originalUri: "/v1/workspaces/1/teams/2/reports");

        Assert.False(result.Succeeded);
        Assert.Equal("invalid personal access token", result.Failure?.Message);
    }

    [Fact]
    public async Task Fail_WhenTokenExpired()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        var expired = UsableToken(
            generated,
            workspaceId: 1,
            teamId: 2,
            expiresAt: _clock.GetUtcNow().AddMinutes(-1));
        _usersClient.Setup(t => t.FindPersonalAccessTokenAsync(generated.Hash)).ReturnsAsync(expired);

        var result = await AuthenticateAsync(
            authorization: $"Bearer {generated.Value}",
            originalUri: "/v1/workspaces/1/teams/2/reports");

        Assert.False(result.Succeeded);
        Assert.Equal("invalid personal access token", result.Failure?.Message);
        _usersClient.Verify(t => t.TouchPersonalAccessTokenAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task Fail_WhenOwnerLostTeamMembership()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        var token = UsableToken(generated, workspaceId: 7, teamId: 8);
        _usersClient.Setup(t => t.FindPersonalAccessTokenAsync(generated.Hash)).ReturnsAsync(token);
        _users.Setup(u => u.GetUserAsync(token.UserId)).ReturnsAsync(new UserContext(
            new AuthUser { Id = token.UserId, ExternalId = "ext", Name = "Pat User" },
            [new Application.Authorization.WorkspaceMember(7, "owner", [999])]));

        var result = await AuthenticateAsync(
            authorization: $"Bearer {generated.Value}",
            originalUri: "/v1/workspaces/7/teams/8/bugs");

        Assert.False(result.Succeeded);
        Assert.Equal("personal access token owner unavailable", result.Failure?.Message);
    }

    [Fact]
    public async Task Fail_WhenScopeMismatchesUrl()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        _usersClient.Setup(t => t.FindPersonalAccessTokenAsync(generated.Hash)).ReturnsAsync(UsableToken(generated, workspaceId: 1, teamId: 2));

        var result = await AuthenticateAsync(
            authorization: $"Bearer {generated.Value}",
            originalUri: "/v1/workspaces/1/teams/99/reports");

        Assert.False(result.Succeeded);
        Assert.Equal("personal access token scope mismatch", result.Failure?.Message);
        _usersClient.Verify(t => t.TouchPersonalAccessTokenAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task Success_SetsAuthHeadersAndTouchesLastUsed()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        var token = UsableToken(generated, workspaceId: 7, teamId: 8);
        _usersClient.Setup(t => t.FindPersonalAccessTokenAsync(generated.Hash)).ReturnsAsync(token);
        _users.Setup(u => u.GetUserAsync(token.UserId)).ReturnsAsync(new UserContext(
            new AuthUser { Id = token.UserId, ExternalId = "ext", Name = "Pat User" },
            [new Application.Authorization.WorkspaceMember(7, "owner", [8])]));

        var context = new DefaultHttpContext();
        var result = await AuthenticateAsync(
            authorization: $"Bearer {generated.Value}",
            originalUri: "/v1/workspaces/7/teams/8/bugs",
            context: context);

        Assert.True(result.Succeeded);
        Assert.Equal(AuthMethods.Pat, result.Principal!.FindFirstValue(AuthClaims.AuthMethod));
        Assert.Equal(token.UserId.ToString(), context.Response.Headers["Auth-Request-User-Id"].ToString());
        Assert.Equal("7", context.Response.Headers["Auth-Request-Workspace-Id"].ToString());
        Assert.Equal("8", context.Response.Headers["Auth-Request-Team-Id"].ToString());
        Assert.Equal("owner", context.Response.Headers["Auth-Request-Workspace-Role"].ToString());
        Assert.Equal(AuthMethods.Pat, context.Response.Headers["Auth-Request-Auth-Method"].ToString());
        _usersClient.Verify(t => t.TouchPersonalAccessTokenAsync(token.Id), Times.Once);
    }

    private async Task<AuthenticateResult> AuthenticateAsync(
        string? authorization,
        string? originalUri = null,
        DefaultHttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        if (originalUri is not null)
        {
            context.Request.Headers["X-Original-URI"] = originalUri;
        }

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string?>())).Returns(new AuthenticationSchemeOptions());

        var handler = new PersonalAccessTokenAuthenticationHandler(
            options.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            _usersClient.Object,
            _users.Object,
            _clock);

        await handler.InitializeAsync(
            new AuthenticationScheme(AuthorizationSchemeNames.Pat, null, typeof(PersonalAccessTokenAuthenticationHandler)),
            context);

        return await handler.AuthenticateAsync();
    }

    private PersonalAccessToken UsableToken(
        GeneratedPersonalAccessToken generated,
        int workspaceId,
        int teamId,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            Id = 55,
            UserId = 42,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            Label = "test",
            TokenPrefix = generated.DisplayPrefix,
            CreatedAt = _clock.GetUtcNow().AddDays(-1),
            ExpiresAt = expiresAt ?? _clock.GetUtcNow().AddDays(30)
        };
}
