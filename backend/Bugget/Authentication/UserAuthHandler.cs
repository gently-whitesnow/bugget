using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugget.DA.Interfaces;
using Bugget.Entities.Authentication;
using Bugget.Entities.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Bugget.Authentication;

public class UserAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<AuthHeadersOptions> authHeadersOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string DefaultUserId = "default-user";
    private readonly string? UserIdHeader = authHeadersOptions.CurrentValue.UserIdHeaderName;
    private readonly string? TeamIdHeader = authHeadersOptions.CurrentValue.TeamIdHeaderName;
    private readonly string? OrganizationIdHeader = authHeadersOptions.CurrentValue.OrganizationIdHeaderName;

    private const string SignalRConnectionIdHeader = "X-Signal-R-Connection-Id";
    private const string ClientNameHeader = "X-Client-Name";
    private const string InternalRoutePrefix = "/v2/_internal/";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Authenticate());

    private AuthenticateResult Authenticate()
    {
        if (Request.Path.Equals("/_internal/ping"))
        {
            return AuthenticateResult.NoResult();
        }

        if (Request.Path.StartsWithSegments("/v2/_internal", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateInternalClient();
        }

        var headers = Request.Headers;

        var userId = GetHeaderOrDefault(headers, UserIdHeader, DefaultUserId);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Fail("User ID not found");
        }

        var teamId = ResolveTeamId(headers);
        if (!string.IsNullOrEmpty(TeamIdHeader) && string.IsNullOrWhiteSpace(teamId))
        {
            return Fail("Team ID not found");
        }

        var organizationId = GetHeaderOrDefault(headers, OrganizationIdHeader);
        if (!string.IsNullOrEmpty(OrganizationIdHeader) && string.IsNullOrWhiteSpace(organizationId))
        {
            return Fail("Organization ID not found");
        }

        var signalRId = GetHeaderOrDefault(headers, SignalRConnectionIdHeader);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(AuthClaims.UserIdHeaderConfigured, (!string.IsNullOrEmpty(UserIdHeader)).ToString().ToLowerInvariant()),
            new(AuthClaims.TeamIdHeaderConfigured, (!string.IsNullOrEmpty(TeamIdHeader)).ToString().ToLowerInvariant()),
            new(AuthClaims.OrganizationIdHeaderConfigured, (!string.IsNullOrEmpty(OrganizationIdHeader)).ToString().ToLowerInvariant())
        };

        if (!string.IsNullOrEmpty(teamId))
        {
            claims.Add(new Claim(AuthClaims.TeamId, teamId));
        }

        if (!string.IsNullOrEmpty(organizationId))
        {
            claims.Add(new Claim(AuthClaims.OrganizationId, organizationId));
        }

        if (!string.IsNullOrEmpty(signalRId))
        {
            claims.Add(new Claim(AuthClaims.SignalRConnectionId, signalRId));
        }

        return AuthenticateResult.Success(CreateTicket(claims));
    }

    private string? ResolveTeamId(IHeaderDictionary headers)
    {
        if (!string.IsNullOrEmpty(TeamIdHeader))
        {
            var teamId = headers[TeamIdHeader].ToString();
            if (!string.IsNullOrWhiteSpace(teamId))
            {
                return teamId;
            }
        }

        return null;
    }

    private static string? GetHeaderOrDefault(IHeaderDictionary headers, string? headerName, string? defaultValue = null)
    {
        if (!string.IsNullOrEmpty(headerName))
        {
            if (headers.TryGetValue(headerName, out var values))
            {
                return values.ToString();
            }

            return null;
        }

        return defaultValue;
    }

    private AuthenticateResult AuthenticateInternalClient()
    {
        var clientName = Request.Headers[ClientNameHeader].ToString();
        if (string.IsNullOrWhiteSpace(clientName))
        {
            Logger.LogWarning("Internal request to {Path} rejected: missing {Header}", Request.Path, ClientNameHeader);
            return Fail($"{ClientNameHeader} header is required");
        }

        Logger.LogInformation("Internal request to {Path} from client {ClientName}", Request.Path, clientName);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, clientName),
            new Claim(AuthClaims.ClientName, clientName),
        };
        return AuthenticateResult.Success(CreateTicket(claims));
    }

    private static AuthenticateResult Fail(string reason) =>
        AuthenticateResult.Fail(reason);

    private AuthenticationTicket CreateTicket(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimsIdentity.DefaultRoleClaimType);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return ticket;
    }
}
