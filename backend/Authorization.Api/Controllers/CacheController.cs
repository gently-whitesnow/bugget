using System.Threading.Tasks;
using Authorization.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authorization.Api.Controllers;

[ApiController]
public class CacheController(
    IUserCache userCache
    ) : ControllerBase
{
    /// <summary>
    /// Внутренний метод для очистки кеша пользователя, используется внутренними сервисами при изменении данных по командам, воркспейсам
    /// </summary>
    /// <returns></returns>
    [HttpDelete("/_internal/users/{userId}/cache")]
    [ProducesResponseType(200)]
    public async Task InvalidateCache(long userId)
    {
        var userIdStr = userId.ToString();

        // Read cached context before deleting — we need the ExternalId
        // to also clear the OIDC-keyed cache entry (keyed by externalId)
        var cached = await userCache.GetUserAsync(userIdStr);

        await userCache.DeleteUserAsync(userIdStr);

        // In OIDC mode the cache is also stored under externalId key;
        // if we don't delete it, stale data persists until TTL expires
        if (!string.IsNullOrEmpty(cached?.User?.ExternalId))
        {
            await userCache.DeleteUserAsync(cached.User.ExternalId);
        }
    }
}
