using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Models;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization;

public sealed class JwkSetRepository : IJwkSetStorage
{
    private readonly ConcurrentDictionary<string, JsonWebKey> _keysById;
    private readonly JwkSetHolder _jwkSetHolder;

    /// <summary>Строит набор JWK из открытых ключей пар RSA; пустой набор — ошибка.</summary>
    public static JwkSetRepository FromRsaKeyPairs(
        IEnumerable<RsaKeyPair> rsaKeyPairs)
    {
        if (rsaKeyPairs == null || !rsaKeyPairs.Any())
        {
            throw new ArgumentNullException(nameof(rsaKeyPairs), "RsaKeyPairs cannot be null or empty.");
        }

        var keys = rsaKeyPairs.Select(key => JsonWebKeyConverter.ConvertFromRSASecurityKey(key.PublicKey))
            .ToList();

        var jwkSetHolder = new JwkSetHolder(keys);
        return new JwkSetRepository(jwkSetHolder);
    }

    private JwkSetRepository(JwkSetHolder jwkSetHolder)
    {
        _jwkSetHolder = jwkSetHolder ?? throw new ArgumentNullException(nameof(jwkSetHolder));
        _keysById = new ConcurrentDictionary<string, JsonWebKey>(
            jwkSetHolder.Keys.ToDictionary(key => key.Kid, key => key));
    }

    public Task<JwkSetHolder> GetJWKSetAsync()
    {
        return Task.FromResult(_jwkSetHolder);
    }

    public Task<JsonWebKey> GetJWKAsync(string kid)
    {
        if (string.IsNullOrWhiteSpace(kid))
        {
            throw new ArgumentException("Key ID (kid) cannot be null or empty.");
        }

        if (_keysById.TryGetValue(kid, out var key))
        {
            return Task.FromResult(key);
        }

        throw new KeyNotFoundException($"JWK with kid '{kid}' not found.");
    }
}
