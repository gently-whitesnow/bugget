using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Models;
/// <summary>
/// Представляет пару RSA ключей (открытый и закрытый) в формате PEM.
/// Используется для подписи и проверки JWT (JSON Web Token).
/// </summary>
public record RsaKeyPair(
    // Идентификатор ключа (Key ID) - уникальный идентификатор для ключа.
    [property: JsonPropertyName("kid")] string KeyId,
    // Закрытый ключ (Private Key) в формате PEM.
    [property: JsonPropertyName("private")] string PrivateKeyPem,
    // Открытый ключ (Public Key) в формате PEM.
    [property: JsonPropertyName("public")] string PublicKeyPem)
{
    private readonly Lazy<RsaSecurityKey> _priv =
        new(() => ParseRsaKey(KeyId, PrivateKeyPem));
    private readonly Lazy<RsaSecurityKey> _pub =
        new(() => ParseRsaKey(KeyId, PublicKeyPem));

    [JsonIgnore] public RsaSecurityKey PrivateKey => _priv.Value;
    [JsonIgnore] public RsaSecurityKey PublicKey => _pub.Value;

    private static RsaSecurityKey ParseRsaKey(string keyId, string rsaKey)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(rsaKey);
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }
}
