using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Models;
using Microsoft.IdentityModel.Tokens;


namespace Bugget.UnitTests.Authorization.TokensService;

public sealed class JwkStorageMock : IJwkSetStorage
{
    private readonly Dictionary<string, JsonWebKey> _index = new();
    public JwkStorageMock(params JsonWebKey[] keys)
    {
        foreach (var k in keys)
        {
            _index[k.Kid!] = k;
        }
    }

    public Task<JsonWebKey> GetJWKAsync(string kid) =>
        _index.TryGetValue(kid, out var jwk) ? Task.FromResult(jwk) :
        throw new KeyNotFoundException($"kid '{kid}' not found");

    public Task<JwkSetHolder> GetJWKSetAsync() =>
        Task.FromResult(new JwkSetHolder(_index.Values.ToList()));
}
