using System.Collections.Concurrent;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;

namespace Bugget.UnitTests.Authorization.TokensService;

public sealed class InMemoryRefreshRotationCache : IRefreshRotationCache
{
    private readonly ConcurrentDictionary<string, (string access, string refresh, DateTimeOffset expiry)> _store = new();

    public Task StoreAsync(string oldJti, string newAccess, string newRefresh, TimeSpan ttl)
    {
        _store[oldJti] = (newAccess, newRefresh, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task<(bool found, string access, string refresh)> TryGetAsync(string oldJti)
    {
        if (_store.TryGetValue(oldJti, out var data) && data.expiry > DateTimeOffset.UtcNow)
        {
            return Task.FromResult((true, data.access, data.refresh));
        }
        return Task.FromResult((false, string.Empty, string.Empty));
    }
}

