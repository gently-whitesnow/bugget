using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Options;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Services;

public sealed class TokensService(
    IOptions<JwtOptions> opts,
    IRsaPrivateKeyStorage accessKeys,
    IRsaPrivateKeyStorage refreshKeys,
    IJwkSetStorage jwks,
    IRefreshRevocationStore revocation,
    IRefreshRotationCache rotationCache,
    TimeProvider timeProvider) : ITokensService
{
    private readonly JwtOptions _opts = opts.Value;

    public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(long userId)
        => await IssuePairAsync(userId);

    public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(
        long userId,
        string refresh)
    {
        // 1) Валидация refresh (БЕЗ проверки revoked) + извлечение JTI/EXP
        var principal = await ValidateRefreshTokenWithoutRevocationCheckAsync(refresh);
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) != userId.ToString())
        {
            throw new SecurityTokenException("userId mismatch");
        }

        var oldJti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti)!;
        var exp = DateTimeOffset.FromUnixTimeSeconds(
            long.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Exp)!));

        // 2) Проверка revoked ПЕРЕД ревокацией
        if (await revocation.IsRevokedAsync(oldJti))
        {
            // Попробуем вернуть из кэша (может быть параллельная ротация)
            var (found, acc, refh) = await rotationCache.TryGetAsync(oldJti);
            if (found)
            {
                return (acc, refh);
            }

            throw new SecurityTokenException("token revoked");
        }

        // 3) Ревокация старого — ровно до той границы, до которой его ещё принимает
        //    lifetime-валидатор (exp + ClockSkew), иначе повторная ротация внутри skew
        //    обошла бы и revocation, и idempotency-кэш.
        await revocation.RevokeAsync(oldJti, RefreshTokenRevocation.RevokedUntil(exp));

        // 4) Выпуск новой пары
        var (access, newRefresh) = await IssuePairAsync(userId);

        // 5) Кэшируем результат ротации под oldJti на короткое время
        var ttl = (exp - timeProvider.GetUtcNow()) + TimeSpan.FromSeconds(30);
        if (ttl < TimeSpan.FromSeconds(30))
        {
            ttl = TimeSpan.FromSeconds(30);
        }

        await rotationCache.StoreAsync(oldJti, access, newRefresh, ttl);

        return (access, newRefresh);
    }

    /* ----------------- helpers ----------------- */

    private async Task<(string access, string refresh)> IssuePairAsync(long userId)
    {
        var (accessJti, access) =
            await SignAsync(userId, accessKeys, _opts.AccessLifetime);
        var (refreshJti, refresh) =
            await SignAsync(userId, refreshKeys, _opts.RefreshLifetime);

        // запись jti не нужна; blacklist only
        return (access, refresh);
    }

    private async Task<(string jti, string token)> SignAsync(
        long userId, IRsaPrivateKeyStorage keys, TimeSpan life)
    {
        var key = await keys.GetRsaPrivateKeyAsync();
        var handler = new JwtSecurityTokenHandler();
        var jti = Guid.NewGuid().ToString("N");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = handler.CreateJwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            subject: new ClaimsIdentity(claims),
            notBefore: now,
            expires: now.Add(life),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha512));

        return (jti, handler.WriteToken(token));
    }

    public async Task<ClaimsPrincipal> ValidateRefreshTokenAsync(string token)
    {
        var principal = await ValidateRefreshTokenWithoutRevocationCheckAsync(token);

        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti)!;
        if (await revocation.IsRevokedAsync(jti))
        {
            throw new SecurityTokenException("token revoked");
        }

        return principal;
    }

    private async Task<ClaimsPrincipal> ValidateRefreshTokenWithoutRevocationCheckAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var jwk = await jwks.GetJWKAsync(jwt.Header.Kid);

        if (!JsonWebKeyConverter.TryConvertToSecurityKey(jwk, out var rsaKey))
        {
            throw new SecurityTokenException("Bad key");
        }

        var prm = new TokenValidationParameters
        {
            ValidIssuer = _opts.Issuer,
            ValidAudience = _opts.Audience,
            IssuerSigningKey = rsaKey,
            ClockSkew = RefreshTokenRevocation.ClockSkew,
            LifetimeValidator = (notBefore, expires, _, parameters) =>
                ValidateLifetime(notBefore, expires, parameters, timeProvider),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true
        };

        return handler.ValidateToken(token, prm, out _);
    }

    private static bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        TokenValidationParameters parameters,
        TimeProvider timeProvider)
    {
        if (!expires.HasValue && parameters.RequireExpirationTime)
        {
            throw new SecurityTokenNoExpirationException();
        }

        if (notBefore.HasValue && expires.HasValue && notBefore > expires)
        {
            throw new SecurityTokenInvalidLifetimeException();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (notBefore > now.Add(parameters.ClockSkew))
        {
            throw new SecurityTokenNotYetValidException();
        }

        if (expires < now.Subtract(parameters.ClockSkew))
        {
            throw new SecurityTokenExpiredException();
        }

        return true;
    }
}
