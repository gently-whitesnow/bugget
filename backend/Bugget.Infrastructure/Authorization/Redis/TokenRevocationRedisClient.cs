using System;
using System.Threading.Tasks;
using Bugget.Application.Authorization.Ports;
using StackExchange.Redis;

namespace Bugget.Infrastructure.Authorization.Redis;

public sealed class TokenRevocationRedisClient(IConnectionMultiplexer mux, TimeProvider timeProvider) : IRefreshRevocationStore
{
    private const string Prefix = "jwt:revoked:";

    /// <summary>
    /// Redis удаляет ключ по истечении TTL, поэтому последний принимаемый валидатором
    /// момент накрывается минимальным ненулевым TTL — той же включительной границей,
    /// что и у in-memory реализации.
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

        return _db.StringSetAsync(key, "1", ttl);
    }

    public Task<bool> IsRevokedAsync(string jti)
        => _db.KeyExistsAsync(Prefix + jti);
}
