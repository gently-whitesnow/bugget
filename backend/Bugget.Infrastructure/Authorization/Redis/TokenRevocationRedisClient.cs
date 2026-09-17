using System;
using System.Globalization;
using System.Threading.Tasks;
using Bugget.Application.Authorization.Ports;
using StackExchange.Redis;

namespace Bugget.Infrastructure.Authorization.Redis;

public sealed class TokenRevocationRedisClient(IConnectionMultiplexer mux, TimeProvider timeProvider) : IRefreshRevocationStore
{
    private const string Prefix = "jwt:revoked:";

    // Отличает запись с границей ревокации от прежнего формата ("1"), который границы не несёт.
    private const string UntilMarker = "until:";

    // TTL — только уборка ключа: он не должен истечь раньше границы revokedUntil, поэтому у наступившей
    // границы остаётся минимальный ненулевой TTL. Ревокацию решает сохранённая граница, а не TTL.
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
