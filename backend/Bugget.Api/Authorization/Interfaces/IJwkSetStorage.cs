
using System.Threading.Tasks;
using Bugget.Api.Authorization.Models;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Interfaces;

/// <summary>Хранилище набора JWK (JSON Web Key).</summary>
public interface IJwkSetStorage
{
    /// <summary>Набор JWK из хранилища; при ошибке бросает исключение.</summary>
    Task<JwkSetHolder> GetJWKSetAsync();

    /// <summary>JWK по идентификатору ключа (kid); при ошибке бросает исключение.</summary>
    Task<JsonWebKey> GetJWKAsync(string kid);
}
