using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Models;

/// <summary>
/// Представляет набор JSON Web Key (JWK), используемый для подписи и проверки JWT (JSON Web Token).
/// </summary>
/// <param name="Keys">Коллекция ключей (keys) - массив объектов JWK, представляющих открытые ключи, <see cref="JsonWebKey"/>.</param>
public record JwkSetHolder(
    /// <summary>
    /// Коллекция ключей (keys) - массив объектов JWK, представляющих открытые ключи.
    /// </summary>
    [property: JsonPropertyName("keys")] IReadOnlyCollection<JsonWebKey> Keys
);
