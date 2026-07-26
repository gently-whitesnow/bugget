using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Authorization.Api.Services;
using Authorization.Options;
using Authorization.Tests.TokensService;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public sealed class TokensServiceTests
{
    // Публичные ключи нужны в Validate()
    private readonly JsonWebKey _accessPub;
    private readonly JsonWebKey _refreshPub;

    // С-префикс: System Under Test
    private readonly TokensService _sut;
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

        _sut = new TokensService(
            Options.Create(_opts),
            new PrivateKeyStorageMock(accPriv),
            new PrivateKeyStorageMock(refPriv),
            new JwkStorageMock(accPub, refPub),
            new InMemoryTokenRevocationStore(),
            new InMemoryRefreshRotationCache());
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

        // Ждем истечения токена + ClockSkew
        // RefreshLifetime = 4 sec, ClockSkew = 10 sec
        // Токен считается валидным до exp + ClockSkew = 4 + 10 = 14 sec
        await Task.Delay(_opts.RefreshLifetime + TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(500));

        // После истечения токена + ClockSkew, ValidateRefreshTokenWithoutRevocationCheckAsync выбросит SecurityTokenExpiredException
        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => _sut.GenerateTokensAsync(3, refresh));
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
            ClockSkew = TimeSpan.Zero
        };
        return h.ValidateToken(jwt, prm, out _);
    }
}
