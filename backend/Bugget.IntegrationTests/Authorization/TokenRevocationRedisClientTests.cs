using System;
using System.Threading.Tasks;
using Bugget.Application.Authorization;
using Bugget.Infrastructure.Authorization.Redis;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Xunit;

namespace Bugget.IntegrationTests.Authorization;

/// <summary>
/// Redis-ревокация берёт время из внедрённого <see cref="TimeProvider"/> и держит запись ровно до
/// <c>exp + ClockSkew</c>, пока валидатор ещё принимает токен — как in-memory реализация.
/// </summary>
[Collection("PostgresCollection")]
public class TokenRevocationRedisClientTests
{
    private readonly ConnectionMultiplexer _mux;
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public TokenRevocationRedisClientTests(RedisContainerFixture redis)
    {
        _mux = ConnectionMultiplexer.Connect(redis.Container.GetConnectionString());
    }

    [Fact(DisplayName = "TTL ревокации считается от внедрённого TimeProvider и равен exp + ClockSkew")]
    public async Task RevokeAsync_UsesInjectedTimeProvider_ForTtl()
    {
        var sut = new TokenRevocationRedisClient(_mux, _timeProvider);
        var jti = $"jti_{Guid.NewGuid():N}";
        var exp = _timeProvider.GetUtcNow().AddMinutes(5);

        await sut.RevokeAsync(jti, RefreshTokenRevocation.RevokedUntil(exp));

        Assert.True(await sut.IsRevokedAsync(jti));

        // Часы теста стоят в 2026 году, реальные — нет: если бы TTL считался от
        // DateTimeOffset.UtcNow, он не попал бы в это окно.
        var ttl = await _mux.GetDatabase().KeyTimeToLiveAsync("jwt:revoked:" + jti);
        Assert.NotNull(ttl);
        var window = TimeSpan.FromMinutes(5) + RefreshTokenRevocation.ClockSkew;
        Assert.InRange(ttl!.Value, window - TimeSpan.FromSeconds(5), window);
    }

    [Fact(DisplayName = "Ревокация не переживает границу exp + ClockSkew")]
    public async Task RevokeAsync_DoesNotOutliveAcceptanceWindow()
    {
        var sut = new TokenRevocationRedisClient(_mux, _timeProvider);
        var jti = $"jti_{Guid.NewGuid():N}";

        // exp уже позади ровно на ClockSkew: валидатор перестаёт принимать токен прямо
        // сейчас, значит и запись о ревокации держать больше нечего.
        var exp = _timeProvider.GetUtcNow() - RefreshTokenRevocation.ClockSkew;
        await sut.RevokeAsync(jti, RefreshTokenRevocation.RevokedUntil(exp));

        var ttl = await _mux.GetDatabase().KeyTimeToLiveAsync("jwt:revoked:" + jti);
        Assert.True(ttl is null || ttl.Value <= TimeSpan.FromMilliseconds(1));
    }

    [Fact(DisplayName = "Redis снимает ревокацию по границе внедрённого TimeProvider")]
    public async Task IsRevokedAsync_UsesInjectedTimeProvider_AtAcceptanceBoundary()
    {
        var sut = new TokenRevocationRedisClient(_mux, _timeProvider);
        var jti = $"jti_{Guid.NewGuid():N}";
        var revokedUntil = _timeProvider.GetUtcNow().AddMinutes(5);

        await sut.RevokeAsync(jti, revokedUntil);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await sut.IsRevokedAsync(jti));

        _timeProvider.Advance(TimeSpan.FromTicks(1));
        Assert.False(await sut.IsRevokedAsync(jti));
    }

    [Fact(DisplayName = "Значение прежнего формата считается отозванным до уборки по TTL")]
    public async Task IsRevokedAsync_TreatsLegacyValue_AsRevoked()
    {
        var sut = new TokenRevocationRedisClient(_mux, _timeProvider);
        var jti = $"jti_{Guid.NewGuid():N}";

        // Ключи прежней версии границы не несут: до истечения их TTL токен остаётся отозванным.
        await _mux.GetDatabase().StringSetAsync("jwt:revoked:" + jti, "1", TimeSpan.FromMinutes(5));

        Assert.True(await sut.IsRevokedAsync(jti));
    }
}
