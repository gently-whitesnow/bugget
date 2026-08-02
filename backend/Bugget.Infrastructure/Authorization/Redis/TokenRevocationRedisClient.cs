using System;
using System.Globalization;
using System.Threading.Tasks;
using Bugget.Application.Authorization.Ports;
using StackExchange.Redis;

namespace Bugget.Infrastructure.Authorization.Redis;

public sealed class TokenRevocationRedisClient(IConnectionMultiplexer mux, TimeProvider timeProvider) : IRefreshRevocationStore
{
    private const string Prefix = "jwt:revoked:";

    /// <summary>
    /// Маркер значения с границей ревокации. Отличает запись нового формата от значения
    /// прежнего формата (<c>"1"</c>), которое границы не несёт.
    /// </summary>
    private const string UntilMarker = "until:";

    /// <summary>
    /// TTL — только физическая уборка ключа: он не должен обнулиться раньше, чем
    /// наступит граница <c>revokedUntil</c>, поэтому у уже наступившей границы остаётся
    /// минимальный ненулевой TTL. Решение о ревокации принимает не он, а сохранённая
    /// граница относительно внедрённого <see cref="TimeProvider"/>.
    /// </summary>
    private static readonly TimeSpan MinTtl = TimeSpan.FromMilliseconds(1);

    private readonly IDatabase _db = mux.GetDatabase();

    public Task RevokeAsync(string jti, DateTimeOffset revokedUntil)
    {
        var key = Prefix + jti;
        var ttl = revokedUntil - timeProvider.GetUtcNow();
        if (ttl < MinTtl)
        {
            ttl = MinTtl;
        }

        var value = UntilMarker + revokedUntil.ToString("O", CultureInfo.InvariantCulture);
        return _db.StringSetAsync(key, value, ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        var stored = await _db.StringGetAsync(Prefix + jti);
        if (!stored.HasValue)
        {
            return false;
        }

        var value = stored.ToString();

        // Значение без границы — запись прежнего формата: считаем токен отозванным,
        // пока Redis не уберёт её по TTL.
        if (!value.StartsWith(UntilMarker, StringComparison.Ordinal)
            || !DateTimeOffset.TryParse(
                value[UntilMarker.Length..],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var revokedUntil))
        {
            return true;
        }

        // Граница включительная — ровно как у in-memory реализации и у валидатора.
        return revokedUntil >= timeProvider.GetUtcNow();
    }
}
