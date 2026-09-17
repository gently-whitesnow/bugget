using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization.Interfaces;

/// <summary>Хранилище секретов: источник закрытого RSA-ключа.</summary>
public interface IRsaPrivateKeyStorage
{
    /// <summary>Получает закрытый ключ из хранилища.</summary>
    Task<RsaSecurityKey> GetRsaPrivateKeyAsync();
}
