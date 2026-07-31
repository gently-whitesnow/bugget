using Authorization.Api.Interfaces;
using Users.BO.Ports;
namespace Bugget.Modules.InProcess;

/// <summary>
/// Инвалидация кэша пользователя из модуля users. Раньше была HTTP-вызовом
/// (<c>DELETE _internal/users/{id}/cache</c> в authorization-api), теперь — прямой вызов кэша.
/// </summary>
/// <remarks>
/// Повторяет логику <c>CacheController</c>: в OIDC-режиме контекст лежит ещё и под ключом
/// externalId, поэтому чистим обе записи.
/// </remarks>
public sealed class AuthorizationCacheAdapter(IUserCache userCache) : IAuthorizationRepository
{
    public async Task InvalidateUserCacheAsync(long userId)
    {
        var userIdStr = userId.ToString();

        var cached = await userCache.GetUserAsync(userIdStr);

        await userCache.DeleteUserAsync(userIdStr);

        if (!string.IsNullOrEmpty(cached?.User?.ExternalId))
        {
            await userCache.DeleteUserAsync(cached.User.ExternalId);
        }
    }
}
