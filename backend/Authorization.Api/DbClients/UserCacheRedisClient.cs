using System.Text.Json;
using System.Threading.Tasks;
using Authorization.Api.Interfaces;
using Authorization.Api.Models;
using Authorization.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Authorization.Api.DbClients;

public sealed class UserCacheRedisClient(IConnectionMultiplexer mux, IOptions<UserCacheOptions> opts) : IUserCache
{
    private const string Prefix = "user:cache:";

    private readonly IDatabase _db = mux.GetDatabase();

    public async Task<UserContext?> GetUserAsync(string idKey)
    {
        var key = GetKey(idKey);
        var user = await _db.StringGetAsync(key);
        if (user.IsNullOrEmpty)
        {
            return null;
        }
        return JsonSerializer.Deserialize<UserContext>((string)user!);
    }

    public Task SetUserAsync(UserContext user, string idKey)
    {
        var key = GetKey(idKey);
        return _db.StringSetAsync(key, JsonSerializer.Serialize(user), opts.Value.ExpirationTime);
    }

    public Task DeleteUserAsync(string idKey)
    {
        var key = GetKey(idKey);
        return _db.KeyDeleteAsync(key);
    }

    private static string GetKey(string id) => Prefix + id;
}
