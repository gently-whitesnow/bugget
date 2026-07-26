using System;
using System.Text.Json;
using System.Threading.Tasks;
using Authorization.Api.Interfaces;
using StackExchange.Redis;

namespace Authorization.Api.DbClients;

public sealed class RefreshRotationRedisCache(IConnectionMultiplexer redis) : IRefreshRotationCache
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task StoreAsync(string oldJti, string newAccess, string newRefresh, TimeSpan ttl)
    {
        var key = $"refresh-rot:{oldJti}";
        var value = JsonSerializer.Serialize(new { access = newAccess, refresh = newRefresh });
        await _db.StringSetAsync(key, value, ttl);
    }

    public async Task<(bool found, string access, string refresh)> TryGetAsync(string oldJti)
    {
        var key = $"refresh-rot:{oldJti}";
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
        {
            return (false, string.Empty, string.Empty);
        }

        var data = JsonSerializer.Deserialize<RotationData>(value.ToString());
        return data is null
            ? (false, string.Empty, string.Empty)
            : (true, data.access, data.refresh);
    }

    private sealed record RotationData(string access, string refresh);
}

