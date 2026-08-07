using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugget.Api.Authorization.Extensions;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Application.Services;
using Bugget.Domain.Authentication;
using Bugget.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bugget.Api.Authorization.Authentication;

/// <summary>
/// Bearer PAT (<c>bgt_pat_*</c>) для nginx <c>auth_request</c>. Identity — владелец токена;
/// <see cref="AuthMethods.Pat"/> уходит в <c>Auth-Request-Auth-Method</c> для P0.
/// </summary>
public sealed class PersonalAccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUsersClient usersClient,
    IUsersService usersService,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Троттлинг по открытому префиксу предъявленного токена: считаются только
    /// неудачи (проверка — до похода в БД, запись — после провала), поэтому
    /// валидный токен агента окно не наполняет, сколько бы запросов он ни делал.
    /// Переполненное окно режет и походы в БД за заведомым мусором. Statics
    /// сознательно: хендлер создаётся на запрос, а окно должно жить дольше.
    /// </summary>
    private static readonly FixedWindowLimiter FailedAttempts =
        new(TimeProvider.System, limit: 10, window: TimeSpan.FromMinutes(5));

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadPatSecret(out var secret))
        {
            return AuthenticateResult.NoResult();
        }

        var attemptKey = secret[..PersonalAccessTokenSecret.DisplayPrefixLength];
        if (FailedAttempts.IsLimited(attemptKey))
        {
            Logger.LogWarning("PAT rate limited: prefix={Prefix}", attemptKey);
            return AuthenticateResult.Fail("personal access token rate limited");
        }

        var token = await usersClient.FindPersonalAccessTokenAsync(PersonalAccessTokenSecret.ComputeHash(secret));
        if (token is null || !token.IsUsable(timeProvider.GetUtcNow()))
        {
            FailedAttempts.Record(attemptKey);
            return AuthenticateResult.Fail("invalid personal access token");
        }

        if (!OriginalUriScope.TryParse(Request.Headers["X-Original-URI"].FirstOrDefault(), out var workspaceId, out var teamId)
            || token.WorkspaceId != workspaceId
            || token.TeamId != teamId)
        {
            Logger.LogInformation(
                "PAT scope mismatch: tokenId={TokenId}, tokenWorkspace={TokenWorkspace}, tokenTeam={TokenTeam}",
                token.Id,
                token.WorkspaceId,
                token.TeamId);
            return AuthenticateResult.Fail("personal access token scope mismatch");
        }

        var user = await usersService.GetUserAsync(token.UserId);
        if (user is null || !HasMembership(user, workspaceId, teamId))
        {
            return AuthenticateResult.Fail("personal access token owner unavailable");
        }

        Context.SetAuthHeaders(user, AuthMethods.Pat);
        await usersClient.TouchPersonalAccessTokenAsync(token.Id);

        Logger.LogInformation(
            "PAT authenticated: tokenId={TokenId}, userId={UserId}, workspaceId={WorkspaceId}, teamId={TeamId}",
            token.Id,
            token.UserId,
            workspaceId,
            teamId);

        return AuthenticateResult.Success(CreateTicket(token.UserId));
    }

    private bool TryReadPatSecret(out string secret)
    {
        secret = string.Empty;
        var authorization = Request.Headers[HeaderNames.Authorization].ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = authorization[BearerPrefix.Length..].Trim();
        if (!PersonalAccessTokenSecret.HasValidFormat(value))
        {
            return false;
        }

        secret = value;
        return true;
    }

    private static bool HasMembership(Application.Authorization.UserContext user, int workspaceId, int teamId)
    {
        var workspace = user.Workspaces?.FirstOrDefault(w => w.WorkspaceId == workspaceId);
        return workspace is not null && workspace.TeamIds.Contains(teamId);
    }

    private AuthenticationTicket CreateTicket(long userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(AuthClaims.AuthMethod, AuthMethods.Pat)
            ],
            Scheme.Name,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    }
}
