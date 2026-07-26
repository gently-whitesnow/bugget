using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Authorization.Api;
using Authorization.Api.Interfaces;
using Authorization.Extensions;
using Authorization.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Api.Services;

public class ConfigureJwtBearerOptions(
    TokenValidationParameters tokenValidationParameters,
    ITokensService tokensService,
    IUsersService usersService,
    ILogger<ConfigureJwtBearerOptions> logger) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly TokenValidationParameters _tokenValidationParameters = tokenValidationParameters;
    private readonly ITokensService _tokensService = tokensService;

    private static readonly TimeSpan ProactiveWindow = TimeSpan.FromSeconds(45);

    private static bool IsAuthSubrequest(HttpContext http)
        => http.Request.Path.StartsWithSegments("/_internal/auth", StringComparison.OrdinalIgnoreCase)
           || string.Equals(http.Request.Headers["X-Auth-Subrequest"], "1", StringComparison.Ordinal);

    private static bool AccessExpiringSoon(string jwt, TimeSpan window, out DateTimeOffset expUtc)
    {
        expUtc = default;
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return false;
        }

        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            var expClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;
            if (expClaim is null)
            {
                return false;
            }

            expUtc = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim));
            return expUtc <= DateTimeOffset.UtcNow.Add(window);
        }
        catch
        {
            return false;
        }
    }

    private static async Task DoSilentRefreshAsync(MessageReceivedContext ctx, string refresh)
    {
        var tokensService = ctx.HttpContext.RequestServices.GetRequiredService<ITokensService>();

        // валидация refresh + userId
        var principal = await tokensService.ValidateRefreshTokenAsync(refresh);
        var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                       ?? principal.FindFirst("nameid")?.Value;
        if (userIdStr is null || !long.TryParse(userIdStr, out var userId))
        {
            throw new SecurityTokenException("refresh has no user");
        }

        // ротация
        var (acc, refh) = await tokensService.GenerateTokensAsync(userId, refresh);

        ApplyTokenPair(ctx, acc, refh);
    }

    private static void ApplyTokenPair(MessageReceivedContext ctx, string access, string refresh)
    {
        var jwtOptions = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;

        // саб-запросу — кастомные хедеры (Nginx сделает Set-Cookie),
        // обычному — реальные Set-Cookie
        if (IsAuthSubrequest(ctx.HttpContext))
        {
            ctx.HttpContext.Response.Headers["X-Auth-Set-Cookie-Access"] =
                HttpContextExtensions.BuildCookieHeader("access_token", access, jwtOptions.AccessLifetime);
            ctx.HttpContext.Response.Headers["X-Auth-Set-Cookie-Refresh"] =
                HttpContextExtensions.BuildCookieHeader("refresh_token", refresh, jwtOptions.RefreshLifetime);
        }
        else
        {
            ctx.HttpContext.SetJsonWebTokensCookie(access, refresh, jwtOptions.AccessLifetime, jwtOptions.RefreshLifetime);
        }

        ctx.Token = access; // кладём новый access в пайплайн
    }

    /// <summary>
    /// Восстановление пары токенов из rotation cache (защита от гонки между
    /// SSR-вызовом /v1/auth и subrequest /_internal/auth: SSR проворачивает ротацию,
    /// браузер не получает новые куки, в кеше под старым jti уже лежит свежая пара).
    /// </summary>
    private static async Task<bool> TryRecoverFromRotationCacheAsync(MessageReceivedContext ctx, string oldRefresh)
    {
        try
        {
            var oldJti = new JwtSecurityTokenHandler().ReadJwtToken(oldRefresh)
                .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            var rotationCache = ctx.HttpContext.RequestServices.GetRequiredService<IRefreshRotationCache>();
            var (found, access, refresh) = await rotationCache.TryGetAsync(oldJti);
            if (!found)
            {
                return false;
            }

            ApplyTokenPair(ctx, access, refresh);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Configure(string name, JwtBearerOptions options)
    {
        if (name != AuthorizationSchemeNames.Jwt)
        {
            return;
        }

        options.TokenValidationParameters = _tokenValidationParameters;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = async ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<ConfigureJwtBearerOptions>>();

                // 1) Bearer
                var authHeader = ctx.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrWhiteSpace(authHeader) &&
                    authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Token = authHeader.Substring("Bearer ".Length).Trim();
                    logger.LogInformation("OnMessageReceived: bearer header token");
                    return;
                }

                // 2) access_token из cookie
                if (ctx.Request.Cookies.TryGetValue("access_token", out var access))
                {
                    // если скоро истекает — сразу рефрешимся
                    if (AccessExpiringSoon(access, ProactiveWindow, out _)
                        && ctx.Request.Cookies.TryGetValue("refresh_token", out var refreshForSoon))
                    {
                        try
                        {
                            await DoSilentRefreshAsync(ctx, refreshForSoon);
                            logger.LogInformation("OnMessageReceived: proactive refresh succeeded");
                            return; // уже положили новый access в ctx.Token
                        }
                        catch (SecurityTokenException ex) when (ex.Message.Contains("revoked", StringComparison.OrdinalIgnoreCase))
                        {
                            if (await TryRecoverFromRotationCacheAsync(ctx, refreshForSoon))
                            {
                                logger.LogInformation("OnMessageReceived: proactive refresh from rotation cache");
                                return;
                            }
                            // кеш не помог — пойдём валидировать старый access (вдруг ещё годен)
                        }
                        catch
                        {
                            /* игнор, пойдём валидировать старый access */
                        }
                    }

                    ctx.Token = access;
                    logger.LogInformation("OnMessageReceived: access_token cookie");
                    return;
                }

                // 3) Пытаемся тихо рефрешнуть по refresh_token
                if (!ctx.Request.Cookies.TryGetValue("refresh_token", out var refresh))
                {
                    return;
                }

                try
                {
                    await DoSilentRefreshAsync(ctx, refresh);
                    logger.LogInformation("OnMessageReceived: silent refresh succeeded");
                }
                catch (SecurityTokenException ex) when (ex.Message.Contains("revoked", StringComparison.OrdinalIgnoreCase))
                {
                    if (await TryRecoverFromRotationCacheAsync(ctx, refresh))
                    {
                        logger.LogInformation("OnMessageReceived: silent refresh from rotation cache");
                        return;
                    }
                    logger.LogInformation("OnMessageReceived: silent refresh failed (revoked, no cache entry)");
                    // пусть будет 401
                }
                catch (Exception ex)
                {
                    logger.LogInformation("OnMessageReceived: silent refresh failed: {Message}", ex.Message);
                    // пусть будет 401
                }
            },

            OnTokenValidated = async ctx =>
            {
                logger.LogInformation("OnTokenValidated: {SecurityToken}", ctx.SecurityToken);
                var principal = ctx.Principal;
                if (principal == null)
                { ctx.Fail("principal is null"); return; }

                var idClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (idClaim == null || !long.TryParse(idClaim.Value, out var userId))
                { ctx.Fail("invalid token"); return; }

                var user = await usersService.GetUserAsync(userId);
                if (user == null)
                { ctx.Fail("user not found"); return; }

                ctx.HttpContext.SetAuthHeaders(user);
            },

            OnAuthenticationFailed = async ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<ConfigureJwtBearerOptions>>();
                logger.LogInformation("OnAuthenticationFailed: {Exception}", ctx.Exception);

                if (ctx.Exception is not SecurityTokenExpiredException)
                {
                    return;
                }

                // В саб-запросе — не трогаем (пусть упадет 401, если не успели обновиться в OnMessageReceived)
                if (IsAuthSubrequest(ctx.HttpContext))
                {
                    return;
                }

                var http = ctx.HttpContext;
                if (!http.Request.Cookies.TryGetValue("refresh_token", out var refresh))
                { ctx.Fail("expired and no refresh"); return; }

                try
                {
                    var tokens = http.RequestServices.GetRequiredService<ITokensService>();
                    var principal = await tokens.ValidateRefreshTokenAsync(refresh);
                    var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                  ?? principal.FindFirst("nameid")?.Value;
                    if (userIdStr is null || !long.TryParse(userIdStr, out var userId))
                    { ctx.Fail("invalid refresh token"); return; }

                    var user = await usersService.GetUserAsync(userId);
                    if (user == null)
                    { ctx.Fail("user not found"); return; }

                    var jwtOpts = http.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;
                    var pair = await _tokensService.GenerateTokensAsync(userId, refresh);

                    http.SetJsonWebTokensCookie(pair.AccessToken, pair.RefreshToken, jwtOpts.AccessLifetime, jwtOpts.RefreshLifetime);
                    http.SetAuthHeaders(user);

                    ctx.Success();
                    logger.LogInformation("SUCCESS_REFRESH: OnAuthenticationFailed: {UserId}", userId);
                }
                catch (SecurityTokenException ex) when (ex.Message.Contains("revoked", StringComparison.OrdinalIgnoreCase))
                {
                    // Попытка получить пару из кэша ротаций при гонке
                    var rotationCache = http.RequestServices.GetRequiredService<IRefreshRotationCache>();
                    try
                    {
                        var oldJti = new JwtSecurityTokenHandler().ReadJwtToken(refresh)
                                       .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
                        var (found, acc, refh) = await rotationCache.TryGetAsync(oldJti);
                        if (found)
                        {
                            var jwtOpts = http.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;
                            http.SetJsonWebTokensCookie(acc, refh, jwtOpts.AccessLifetime, jwtOpts.RefreshLifetime);

                            // Получим userId для SetAuthHeaders
                            var handler = new JwtSecurityTokenHandler();
                            var newPrincipal = handler.ReadJwtToken(acc);
                            var userIdStr = newPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
                            if (userIdStr != null && long.TryParse(userIdStr, out var userId))
                            {
                                var user = await usersService.GetUserAsync(userId);
                                if (user != null)
                                {
                                    http.SetAuthHeaders(user);
                                }
                            }

                            ctx.Success();
                            logger.LogInformation("SUCCESS_REFRESH: OnAuthenticationFailed from rotation cache");
                            return;
                        }
                    }
                    catch
                    {
                        // игнор
                    }
                    ctx.Fail("refresh failed: token revoked");
                }
                catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
                {
                    ctx.Fail("refresh failed");
                }
            }
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(AuthorizationSchemeNames.Jwt, options);
}
