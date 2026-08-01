using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Oidc;

public sealed class OidcTokenValidator : IOidcTokenValidator
{
    private readonly OidcAuthOptions _options;
    private readonly ILogger<OidcTokenValidator> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public OidcTokenValidator(IOptions<OidcAuthOptions> options, ILogger<OidcTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();

        // Setup OIDC configuration manager for automatic JWKS discovery and caching
        var documentRetriever = new HttpDocumentRetriever
        {
            RequireHttps = _options.RequireHttpsMetadata
        };

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            // Get OIDC configuration (cached, auto-refreshed)
            var config = await _configManager.GetConfigurationAsync(ct);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = _options.ValidateAudience && !string.IsNullOrEmpty(_options.Audience),
                ValidAudience = _options.Audience,
                ValidateLifetime = _options.ValidateLifetime,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = config.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogDebug(ex, "OIDC token expired");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "OIDC token validation failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating OIDC token");
            return null;
        }
    }

    public string? GetSubject(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrEmpty(_options.IdKey))
        {
            var value = principal.FindFirstValue(_options.IdKey);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? principal.FindFirstValue("sub");
    }
}
