using System;
using System.Threading.Tasks;

namespace Bugget.Application.Authorization.Ports;

/// <summary>Кэш результатов ротации refresh-токенов: устраняет гонки параллельных запросов
/// с одним и тем же старым refresh-токеном.</summary>
public interface IRefreshRotationCache
{
    /// <summary>Сохраняет новую пару токенов по старому JTI.</summary>
    Task StoreAsync(string oldJti, string newAccess, string newRefresh, TimeSpan ttl);

    Task<(bool found, string access, string refresh)> TryGetAsync(string oldJti);
}

