using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Users.Authentication;

public class UserAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<AuthHeadersOptions> authHeadersOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly string UserIdHeader = authHeadersOptions.CurrentValue.UserIdHeaderName!;
    private readonly string TeamIdHeader = authHeadersOptions.CurrentValue.TeamIdHeaderName!;
    private readonly string WorkspaceIdHeader = authHeadersOptions.CurrentValue.WorkspaceIdHeaderName!;
    private readonly string RoleHeader = authHeadersOptions.CurrentValue.WorkspaceRoleHeaderName!;
    private readonly string? AuthMethodHeader = authHeadersOptions.CurrentValue.AuthMethodHeaderName;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers;

        var userId = GetHeader(headers, UserIdHeader);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(Fail("User ID not found"));
        }

        var teamId = GetHeader(headers, TeamIdHeader);
        var workspaceId = GetHeader(headers, WorkspaceIdHeader);
        var role = GetHeader(headers, RoleHeader);

        // Логирование для отладки
        Logger.LogInformation("UserAuthHandler: UserId={UserId}, WorkspaceId={WorkspaceId}, TeamId={TeamId}, Role={Role}",
            userId, workspaceId, teamId, role);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role ?? WorkspaceRole.Member)
        };

        if (!string.IsNullOrEmpty(teamId))
        {
            claims.Add(new Claim(ClaimKey.Team, teamId));
        }

        if (!string.IsNullOrEmpty(workspaceId))
        {
            claims.Add(new Claim(ClaimKey.Workspace, workspaceId));
        }

        var authMethod = GetHeader(headers, AuthMethodHeader);
        if (!string.IsNullOrEmpty(authMethod))
        {
            claims.Add(new Claim(AuthClaims.AuthMethod, authMethod));
        }

        Logger.LogInformation("UserAuthHandler: Created claims: {Claims}",
            string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}")));

        return Task.FromResult(AuthenticateResult.Success(CreateTicket(claims)));
    }

    private static string? GetHeader(IHeaderDictionary headers, string? headerName)
    {
        if (!string.IsNullOrEmpty(headerName))
        {
            if (headers.TryGetValue(headerName, out var values))
            {
                return values.ToString();
            }

            return null;
        }

        return null;
    }

    private static AuthenticateResult Fail(string reason) =>
        AuthenticateResult.Fail(reason);

    private AuthenticationTicket CreateTicket(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return ticket;
    }
}
