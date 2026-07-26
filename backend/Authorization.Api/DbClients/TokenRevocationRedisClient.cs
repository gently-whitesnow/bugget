using System;
using System.Threading.Tasks;
using Authorization.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Authorization.Api.DbClients;

public sealed class TokenRevocationRedisClient(IConnectionMultiplexer mux, IOptions<JwtOptions> opts) : IRefreshRevocationStore
{
    private const string Prefix = "jwt:revoked:";
    private readonly IDatabase _db = mux.GetDatabase();

    public Task RevokeAsync(string jti, DateTimeOffset exp)
    {
        var key = Prefix + jti;
        var ttl = exp - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = opts.Value.AccessLifetime;
        }

        return _db.StringSetAsync(key, "1", ttl);
    }

    public Task<bool> IsRevokedAsync(string jti)
        => _db.KeyExistsAsync(Prefix + jti);
}
