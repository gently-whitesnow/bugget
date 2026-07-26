
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Interfaces;
using Authorization.Api.Models;
using Authorization.Extensions;
using Authorization.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Authorization;

public sealed class ExternalAuthService(
    IUsersService userService,
    ITokensService tokensService,
    IOptions<JwtOptions> jwtOptions,
    TokenValidationParameters tokenValidationParameters,
    ILogger<ExternalAuthService> logger) : IExternalAuthService
{
    public async Task AuthorizeAsync(
        HttpContext context,
        IExternalUser externalUser,
        bool useExternalTokens = false,
        string? provider = null,
        string? email = null)
    {
        User result;

        if (!string.IsNullOrWhiteSpace(provider))
        {
            var existingUserId = await userService.FindUserByProviderAndExternalIdAsync(provider, externalUser.ExternalId);
            result = existingUserId is not null
                ? new User { Id = existingUserId.Value }
                : await userService.InsertOrUpdateUserAsync(externalUser);

            var (success, errorCode, conflictOwnerId) = await userService.AddExternalLinkAsync(
                result.Id,
                provider,
                externalUser.ExternalId,
                email);

            if (!success && errorCode == "external_id_taken" && long.TryParse(conflictOwnerId, out var ownerId))
            {
                if (ownerId != result.Id)
                {
                    logger.LogWarning(
                        "External link {Provider}/{ExternalId} already belongs to user {OwnerId}, switching auth result from user {UserId}",
                        provider,
                        externalUser.ExternalId,
                        ownerId,
                        result.Id);
                }

                result = new User { Id = ownerId };
            }
        }
        else
        {
            result = await userService.InsertOrUpdateUserAsync(externalUser);
        }

        if (useExternalTokens)
        {
            return;
        }
        var authResult = await tokensService.GenerateTokensAsync(result.Id);
        var (accessToken, refreshToken) = authResult;

        context.SetJsonWebTokensCookie(accessToken, refreshToken, jwtOptions.Value.AccessLifetime, jwtOptions.Value.RefreshLifetime);
    }

    public async Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> LinkAccountAsync(
        HttpContext context, string provider, string externalId, string? email)
    {
        var userId = ExtractUserIdFromCookie(context);
        if (userId is null)
        {
            return (false, "not_authenticated", null);
        }

        logger.LogInformation("Linking {Provider} account (externalId={ExternalId}) to user {UserId}",
            provider, externalId, userId);

        return await userService.AddExternalLinkAsync(userId.Value, provider, externalId, email);
    }

    private long? ExtractUserIdFromCookie(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue("access_token", out var accessToken)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken, tokenValidationParameters, out _);
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirst("sub");
            if (idClaim is not null && long.TryParse(idClaim.Value, out var userId))
            {
                return userId;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate access_token cookie for link mode");
        }

        return null;
    }
}
