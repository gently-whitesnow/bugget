using System.Collections.Concurrent;

namespace Authorization.Tests.TokensService;

public sealed class InMemoryTokenRevocationStore : IRefreshRevocationStore
{
    // jti -> expiry
    private readonly ConcurrentDictionary<string, DateTimeOffset> _store = new();
    public Task<bool> IsRevokedAsync(string jti)
        => Task.FromResult(_store.TryGetValue(jti, out var exp) && exp > DateTimeOffset.UtcNow);

    public Task RevokeAsync(string jti, DateTimeOffset expires)
    {
        _store[jti] = expires;
        return Task.CompletedTask;
    }
}
