using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Models;
/// <summary>
/// Представляет пару RSA ключей (открытый и закрытый) в формате PEM.
/// Используется для подписи и проверки JWT (JSON Web Token).
/// </summary>
public record RsaKeyPair(
    /// <summary>
    /// Идентификатор ключа (Key ID) - уникальный идентификатор для ключа.
    /// </summary>
    [property: JsonPropertyName("kid")] string KeyId,
    /// <summary>
    /// Закрытый ключ (Private Key) в формате PEM.
    /// </summary>
    [property: JsonPropertyName("private")] string PrivateKeyPem,
    /// <summary>
    /// Открытый ключ (Public Key) в формате PEM.
    /// </summary>
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
