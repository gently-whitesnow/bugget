
using System.Threading.Tasks;
using Authorization.Models;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Interfaces;

/// <summary>
/// Интерфейс для работы с хранилищем набора JWK (JSON Web Key).
/// </summary>
public interface IJwkSetStorage
{
    /// <summary>
    /// Получает набор JWK (JSON Web Key) из хранилища.
    /// </summary>
    /// <returns>Набор JWK, <see cref="JwkSet"/>.</returns>
    /// <remarks>
    /// Если набор JWK успешно получен, возвращает набор JWK.
    /// В случае ошибки возвращает выброс ошибки.
    /// </remarks>
    public Task<JwkSetHolder> GetJWKSetAsync();

    /// <summary>
    /// Получает JWK (JSON Web Key) по указанному идентификатору ключа (kid).
    /// </summary>
    /// <param name="kid">Идентификатор ключа (Key ID), используемый для поиска конкретного JWK.</param>
    /// <returns>JWK, <see cref="JsonWebKey"/>.</returns>
    /// <remarks>
    /// Если JWK успешно получен, возвращает, содержащий JWK.
    /// В случае ошибки возвращает выброс ошибки.
    /// </remarks>
    public Task<JsonWebKey> GetJWKAsync(string kid);
}
