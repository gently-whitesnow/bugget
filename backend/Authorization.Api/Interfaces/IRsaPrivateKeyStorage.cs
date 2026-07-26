using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Interfaces;

/// <summary>
/// Интерфейс для работы с хранилищем секретов, например, для получения закрытого ключа.
/// </summary>
public interface IRsaPrivateKeyStorage
{
    /// <summary>
    /// Получает закрытый ключ из хранилища.
    /// </summary>
    /// <returns>Закрытый ключ в виде <see cref="RsaSecurityKey"/>.</returns>
    /// <remarks>
    /// Если закрытый ключ успешно получен, возвращает <see cref="Result{T}"/>, содержащий закрытый ключ.
    /// В случае ошибки возвращает <see cref="Result{T}"/> с деталями ошибки.
    /// </remarks>
    public Task<RsaSecurityKey> GetRsaPrivateKeyAsync();
}
