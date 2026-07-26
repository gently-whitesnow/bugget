using System.IdentityModel.Tokens.Jwt;
using Authorization.Api;
using Authorization.Api.Interfaces;
using Authorization.Api.Services;
using Authorization.Options;
using Authorization.Tests.TokensService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OptionsHelper = Microsoft.Extensions.Options.Options;

namespace Authorization.Tests;

/// <summary>
/// Прямые тесты на JwtBearerEvents.OnMessageReceived из ConfigureJwtBearerOptions.
/// Покрывает rotation-cache fallback в обеих ветках (есть access / нет access),
/// чтобы исключить регрессию реального бага: SSR-вызов /v1/auth (не subrequest)
/// проворачивает refresh, а subrequest /_internal/auth с тем же старым refresh
/// должен поднять свежую пару из IRefreshRotationCache.
/// </summary>
public sealed class ConfigureJwtBearerOptionsTests
{
    private readonly JwtOptions _opts = new()
    {
        Issuer = "https://auth.test",
        Audience = "https://api.test",
        AccessLifetime = TimeSpan.FromSeconds(2),
        RefreshLifetime = TimeSpan.FromMinutes(30),
    };

    private readonly InMemoryRefreshRotationCache _cache = new();
    private readonly InMemoryTokenRevocationStore _revocation = new();
    private readonly Authorization.Api.Services.TokensService _tokens;
    private readonly JwtBearerEvents _events;
    private readonly IServiceProvider _sp;

    public ConfigureJwtBearerOptionsTests()
    {
        var (accPriv, accPub) = RsaKeysMock.Create("kid-access");
        var (refPriv, refPub) = RsaKeysMock.Create("kid-refresh");

        _tokens = new Authorization.Api.Services.TokensService(
            OptionsHelper.Create(_opts),
            new PrivateKeyStorageMock(accPriv),
            new PrivateKeyStorageMock(refPriv),
            new JwkStorageMock(accPub, refPub),
            _revocation,
            _cache);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(OptionsHelper.Create(_opts));
        services.AddSingleton<ITokensService>(_tokens);
        services.AddSingleton<IRefreshRotationCache>(_cache);
        _sp = services.BuildServiceProvider();

        var sut = new ConfigureJwtBearerOptions(
            new TokenValidationParameters(),
            _tokens,
            new Mock<IUsersService>().Object,
            NullLogger<ConfigureJwtBearerOptions>.Instance);

        var jwtOpts = new JwtBearerOptions();
        sut.Configure(AuthorizationSchemeNames.Jwt, jwtOpts);
        _events = jwtOpts.Events!;
    }

    [Fact]
    public async Task NoAccessCookie_RevokedRefresh_RotationCacheHit_Subrequest_WritesXAuthHeaders()
    {
        var (_, originalRefresh) = await _tokens.GenerateTokensAsync(42);
        // Ротация: revoke originalRefresh + cache (origJti) -> (newAcc, newRefh)
        var (newAcc, newRefh) = await _tokens.GenerateTokensAsync(42, originalRefresh);

        var http = MakeContext(new() { ["refresh_token"] = originalRefresh }, subrequest: true);
        var ctx = await InvokeOnMessageReceivedAsync(http);

        Assert.Equal(newAcc, ctx.Token);
        Assert.Contains($"access_token={newAcc}", http.Response.Headers["X-Auth-Set-Cookie-Access"].ToString());
        Assert.Contains($"refresh_token={newRefh}", http.Response.Headers["X-Auth-Set-Cookie-Refresh"].ToString());
        Assert.False(http.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task NoAccessCookie_RevokedRefresh_RotationCacheHit_NonSubrequest_WritesSetCookie()
    {
        var (_, originalRefresh) = await _tokens.GenerateTokensAsync(42);
        var (newAcc, newRefh) = await _tokens.GenerateTokensAsync(42, originalRefresh);

        var http = MakeContext(new() { ["refresh_token"] = originalRefresh }, subrequest: false);
        var ctx = await InvokeOnMessageReceivedAsync(http);

        Assert.Equal(newAcc, ctx.Token);
        var setCookies = http.Response.Headers["Set-Cookie"].ToArray();
        Assert.Contains(setCookies, c => c is not null && c.Contains($"access_token={newAcc}"));
        Assert.Contains(setCookies, c => c is not null && c.Contains($"refresh_token={newRefh}"));
        Assert.False(http.Response.Headers.ContainsKey("X-Auth-Set-Cookie-Access"));
    }

    [Fact]
    public async Task NoAccessCookie_RevokedRefresh_RotationCacheMiss_LeavesTokenUnset()
    {
        var (_, originalRefresh) = await _tokens.GenerateTokensAsync(42);
        // Ревок без кэширования (имитация TTL-эвикции / неотносящегося revoke)
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(originalRefresh);
        var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        await _revocation.RevokeAsync(jti, DateTimeOffset.UtcNow.AddHours(1));

        var http = MakeContext(new() { ["refresh_token"] = originalRefresh }, subrequest: true);
        var ctx = await InvokeOnMessageReceivedAsync(http);

        Assert.Null(ctx.Token);
        Assert.False(http.Response.Headers.ContainsKey("X-Auth-Set-Cookie-Access"));
        Assert.False(http.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task NoAccessCookie_ValidRefresh_SilentRefreshIssuesNewPair()
    {
        var (originalAcc, refresh) = await _tokens.GenerateTokensAsync(99);

        var http = MakeContext(new() { ["refresh_token"] = refresh }, subrequest: true);
        var ctx = await InvokeOnMessageReceivedAsync(http);

        Assert.NotNull(ctx.Token);
        Assert.NotEqual(originalAcc, ctx.Token);
        Assert.True(http.Response.Headers.ContainsKey("X-Auth-Set-Cookie-Access"));
        Assert.True(http.Response.Headers.ContainsKey("X-Auth-Set-Cookie-Refresh"));
    }

    [Fact]
    public async Task AccessExpiringSoon_RevokedRefresh_RotationCacheHit_RecoversInUpperBranch()
    {
        // AccessLifetime=2s, ProactiveWindow=45s — access автоматически попадает в "expiring soon"
        var (originalAcc, originalRefresh) = await _tokens.GenerateTokensAsync(7);
        var (newAcc, _) = await _tokens.GenerateTokensAsync(7, originalRefresh);

        var http = MakeContext(
            new() { ["access_token"] = originalAcc, ["refresh_token"] = originalRefresh },
            subrequest: true);
        var ctx = await InvokeOnMessageReceivedAsync(http);

        Assert.Equal(newAcc, ctx.Token);
        Assert.True(http.Response.Headers.ContainsKey("X-Auth-Set-Cookie-Access"));
    }

    /* ----------------- helpers ----------------- */

    private DefaultHttpContext MakeContext(Dictionary<string, string> cookies, bool subrequest)
    {
        var http = new DefaultHttpContext { RequestServices = _sp };
        if (cookies.Count > 0)
        {
            http.Request.Headers["Cookie"] =
                string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        if (subrequest)
        {
            http.Request.Headers["X-Auth-Subrequest"] = "1";
        }
        return http;
    }

    private async Task<MessageReceivedContext> InvokeOnMessageReceivedAsync(HttpContext http)
    {
        var scheme = new AuthenticationScheme(
            AuthorizationSchemeNames.Jwt,
            displayName: null,
            handlerType: typeof(JwtBearerHandler));
        var ctx = new MessageReceivedContext(http, scheme, new JwtBearerOptions());
        await _events.MessageReceived(ctx);
        return ctx;
    }
}
