using System;
using System.Threading.Tasks;

namespace Bugget.Application.Authorization.Ports;

/// <summary>
/// Кэш для хранения результатов ротации refresh токенов.
/// Используется для устранения гонок при параллельных запросах с одним и тем же старым refresh токеном.
/// </summary>
public interface IRefreshRotationCache
{
    /// <summary>
    /// Сохраняет результат ротации токенов (новую пару) по старому JTI.
    /// </summary>
    Task StoreAsync(string oldJti, string newAccess, string newRefresh, TimeSpan ttl);

    /// <summary>
    /// Пытается получить результат предыдущей ротации по старому JTI.
    /// </summary>
    Task<(bool found, string access, string refresh)> TryGetAsync(string oldJti);
}

