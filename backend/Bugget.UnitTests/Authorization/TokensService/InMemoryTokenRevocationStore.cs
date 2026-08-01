using System.Collections.Concurrent;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;

namespace Bugget.UnitTests.Authorization.TokensService;

public sealed class InMemoryTokenRevocationStore(TimeProvider? timeProvider = null) : IRefreshRevocationStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    // jti -> expiry
    private readonly ConcurrentDictionary<string, DateTimeOffset> _store = new();
    public Task<bool> IsRevokedAsync(string jti)
        => Task.FromResult(_store.TryGetValue(jti, out var exp) && exp > _timeProvider.GetUtcNow());

    public Task RevokeAsync(string jti, DateTimeOffset expires)
    {
        _store[jti] = expires;
        return Task.CompletedTask;
    }
}
