using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bugget.Api.Authorization.Options;
using Bugget.Api.Authorization.Services;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.UnitTests.Authorization.TokensService;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

public sealed class TokensServiceTests
{
    // Публичные ключи нужны в Validate()
    private readonly JsonWebKey _accessPub;
    private readonly JsonWebKey _refreshPub;
    private readonly RsaSecurityKey _refreshPrivateKey;

    // С-префикс: System Under Test
    private readonly TokensService _sut;
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly JwtOptions _opts = new()
    {
        Issuer = "https://auth.test",
        Audience = "https://api.test",
        AccessLifetime = TimeSpan.FromSeconds(2),
        RefreshLifetime = TimeSpan.FromSeconds(4)
    };

    public TokensServiceTests()
    {
        var (accPriv, accPub) = RsaKeysMock.Create("kid-access");
        var (refPriv, refPub) = RsaKeysMock.Create("kid-refresh");
        _accessPub = accPub;
        _refreshPub = refPub;
        _refreshPrivateKey = refPriv;

        _sut = new TokensService(
            Options.Create(_opts),
            new PrivateKeyStorageMock(accPriv),
            new PrivateKeyStorageMock(refPriv),
            new JwkStorageMock(accPub, refPub),
            new InMemoryTokenRevocationStore(_timeProvider),
            new InMemoryRefreshRotationCache(_timeProvider),
            _timeProvider);
    }

    /* ---------- позитивные сценарии ---------- */

    [Fact]
    public async Task AccessToken_Validates_With_Correct_PublicKey()
    {
        var (access, _) = await _sut.GenerateTokensAsync(1);
        var p = Validate(access, _accessPub);
        Assert.Equal("1", p.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task RefreshToken_Validates_With_Correct_PublicKey()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(5);
        var p = Validate(refresh, _refreshPub);
        Assert.Equal("5", p.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    /* ---------- негативные сценарии ---------- */

    [Fact]
    public async Task Wrong_PublicKey_Fails_Validation()
    {
        var (access, _) = await _sut.GenerateTokensAsync(1);

        await Assert.ThrowsAsync<SecurityTokenSignatureKeyNotFoundException>(
            () => Task.FromResult(Validate(access, _refreshPub)));
    }

    [Fact]
    public async Task Expired_RefreshToken_Cannot_Be_Reused()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(3);

        _timeProvider.Advance(
            _opts.RefreshLifetime + TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(500));

        // После истечения токена + ClockSkew, ValidateRefreshTokenWithoutRevocationCheckAsync выбросит SecurityTokenExpiredException
        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => _sut.GenerateTokensAsync(3, refresh));
    }

    [Fact]
    public async Task RefreshToken_Is_Valid_At_Expiration_Plus_ClockSkew_And_Expires_After_Boundary()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(3);

        _timeProvider.Advance(_opts.RefreshLifetime + TimeSpan.FromSeconds(10));

        var principal = await _sut.ValidateRefreshTokenAsync(refresh);
        Assert.Equal("3", principal.FindFirstValue(ClaimTypes.NameIdentifier));

        _timeProvider.Advance(TimeSpan.FromTicks(1));

        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => _sut.ValidateRefreshTokenAsync(refresh));
    }

    [Fact]
    public async Task RefreshToken_With_Future_NotBefore_Throws_NotYetValid()
    {
        var refresh = CreateRefreshToken(
            notBefore: _timeProvider.GetUtcNow().AddSeconds(11),
            expires: _timeProvider.GetUtcNow().AddMinutes(1));

        await Assert.ThrowsAsync<SecurityTokenNotYetValidException>(
            () => _sut.ValidateRefreshTokenAsync(refresh));
    }

    [Fact]
    public async Task RefreshToken_Without_Expiration_Throws_NoExpiration()
    {
        var refresh = CreateRefreshToken(
            notBefore: _timeProvider.GetUtcNow(),
            expires: null);

        await Assert.ThrowsAsync<SecurityTokenNoExpirationException>(
            () => _sut.ValidateRefreshTokenAsync(refresh));
    }

    [Fact]
    public async Task RefreshToken_With_NotBefore_After_Expiration_Throws_InvalidLifetime()
    {
        var refresh = CreateRefreshToken(
            notBefore: _timeProvider.GetUtcNow().AddMinutes(1),
            expires: _timeProvider.GetUtcNow().AddSeconds(30));

        await Assert.ThrowsAsync<SecurityTokenInvalidLifetimeException>(
            () => _sut.ValidateRefreshTokenAsync(refresh));
    }

    [Fact]
    public async Task Old_RefreshToken_Revoked_After_Rotation()
    {
        var (_, refresh1) = await _sut.GenerateTokensAsync(9);
        var (access2, refresh2) = await _sut.GenerateTokensAsync(9, refresh1); // обновили ⇒ refresh1 в blacklist

        // Немедленная попытка использовать refresh1 снова (параллельная гонка)
        // Должна вернуть ту же пару из кэша
        var (cachedAccess, cachedRefresh) = await _sut.GenerateTokensAsync(9, refresh1);
        Assert.Equal(access2, cachedAccess);
        Assert.Equal(refresh2, cachedRefresh);

        // второй токен всё ещё валиден
        var ok = await _sut.GenerateTokensAsync(9, refresh2);
        Assert.False(string.IsNullOrWhiteSpace(ok.AccessToken));
    }

    [Fact]
    public async Task Concurrent_Refresh_Returns_Same_Tokens_From_Cache()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(7);

        // Первый запрос делает ротацию
        var (access1, refresh1) = await _sut.GenerateTokensAsync(7, refresh);

        // Второй запрос с тем же старым refresh должен получить те же токены из кэша
        var (access2, refresh2) = await _sut.GenerateTokensAsync(7, refresh);

        Assert.Equal(access1, access2);
        Assert.Equal(refresh1, refresh2);
    }

    [Fact]
    public async Task Repeated_Rotation_During_ClockSkew_Returns_Cached_Tokens()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(7);
        var firstRotation = await _sut.GenerateTokensAsync(7, refresh);

        _timeProvider.Advance(_opts.RefreshLifetime);

        var repeatedRotation = await _sut.GenerateTokensAsync(7, refresh);

        Assert.Equal(firstRotation, repeatedRotation);
    }

    [Fact]
    public async Task Repeated_Rotation_At_ClockSkew_Boundary_Returns_Cached_Tokens()
    {
        var (_, refresh) = await _sut.GenerateTokensAsync(7);
        var firstRotation = await _sut.GenerateTokensAsync(7, refresh);

        // Последний момент, который lifetime-валидатор ещё принимает.
        _timeProvider.Advance(_opts.RefreshLifetime + RefreshTokenRevocation.ClockSkew);

        Assert.Equal(firstRotation, await _sut.GenerateTokensAsync(7, refresh));

        // За границей окна токен отклоняется по времени жизни, а не выпускает вторую пару.
        _timeProvider.Advance(TimeSpan.FromTicks(1));

        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => _sut.GenerateTokensAsync(7, refresh));
    }

    /* ---------- helper ---------- */

    private ClaimsPrincipal Validate(string jwt, JsonWebKey pubKey)
    {
        var h = new JwtSecurityTokenHandler();
        var prm = new TokenValidationParameters
        {
            ValidIssuer = _opts.Issuer,
            ValidAudience = _opts.Audience,
            IssuerSigningKey = pubKey,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
                notBefore <= _timeProvider.GetUtcNow().UtcDateTime
                && expires >= _timeProvider.GetUtcNow().UtcDateTime
        };
        return h.ValidateToken(jwt, prm, out _);
    }

    private string CreateRefreshToken(DateTimeOffset? notBefore, DateTimeOffset? expires)
    {
        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iss, _opts.Issuer },
            { JwtRegisteredClaimNames.Aud, _opts.Audience },
            { ClaimTypes.NameIdentifier, "3" },
            { JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N") }
        };

        if (notBefore.HasValue)
        {
            payload.Add(
                JwtRegisteredClaimNames.Nbf,
                EpochTime.GetIntDate(notBefore.Value.UtcDateTime));
        }

        if (expires.HasValue)
        {
            payload.Add(
                JwtRegisteredClaimNames.Exp,
                EpochTime.GetIntDate(expires.Value.UtcDateTime));
        }

        var token = new JwtSecurityToken(
            new JwtHeader(new SigningCredentials(_refreshPrivateKey, SecurityAlgorithms.RsaSha512)),
            payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
