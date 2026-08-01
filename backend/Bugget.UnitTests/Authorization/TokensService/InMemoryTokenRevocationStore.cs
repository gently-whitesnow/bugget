using System.Collections.Concurrent;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;

namespace Bugget.UnitTests.Authorization.TokensService;

public sealed class InMemoryTokenRevocationStore(TimeProvider? timeProvider = null) : IRefreshRevocationStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    // jti -> момент, до которого включительно токен считается отозванным
    private readonly ConcurrentDictionary<string, DateTimeOffset> _store = new();

    // Граница включительная: пока валидатор ещё принимает токен, он остаётся отозванным.
    public Task<bool> IsRevokedAsync(string jti)
        => Task.FromResult(_store.TryGetValue(jti, out var revokedUntil)
                           && revokedUntil >= _timeProvider.GetUtcNow());

    public Task RevokeAsync(string jti, DateTimeOffset revokedUntil)
    {
        _store[jti] = revokedUntil;
        return Task.CompletedTask;
    }
}
