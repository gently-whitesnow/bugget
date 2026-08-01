using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Interfaces;

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
    /// Если закрытый ключ успешно получен, возвращает результат, содержащий закрытый ключ.
    /// В случае ошибки возвращает результат с деталями ошибки.
    /// </remarks>
    public Task<RsaSecurityKey> GetRsaPrivateKeyAsync();
}
