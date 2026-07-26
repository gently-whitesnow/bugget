using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Interfaces;
using Authorization.Api.Models;
using Microsoft.Extensions.Logging;

namespace Authorization.Api.Services;

public sealed class UsersService(IUsersClient usersClient, IUserCache userCache, ILogger<UsersService> logger) : IUsersService
{
    public async Task<UserContext?> GetUserAsync(long id)
    {
        var cachedUser = await userCache.GetUserAsync(id.ToString());
        if (cachedUser != null)
        {
            return cachedUser;
        }
        var result = await usersClient.GetUserContextAsync(id);
        if (result.HasError)
        {
            logger.LogError("Failed to get user from service: {Error}", result.Error);
            return null;
        }

        await userCache.SetUserAsync(result.Value!, id.ToString());

        return result.Value;
    }

    public async Task<UserContext?> GetUserByExternalIdAsync(string externalId)
    {
        var cachedUser = await userCache.GetUserAsync(externalId);
        if (cachedUser != null)
        {
            return cachedUser;
        }

        var result = await usersClient.GetUserContextByExternalIdAsync(externalId);
        if (result.HasError)
        {
            logger.LogError("Failed to get user by externalId from service: {Error}", result.Error);
            return null;
        }

        await userCache.SetUserAsync(result.Value!, externalId);

        // Also cache by numeric userId so that InvalidateUserCacheAsync(userId)
        // can find the entry and discover the externalId for cleanup
        await userCache.SetUserAsync(result.Value!, result.Value.User.Id.ToString());

        return result.Value;
    }

    public Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId)
    {
        return usersClient.FindUserByProviderAndExternalIdAsync(provider, externalId);
    }

    public Task<bool> IsAdminAsync(long userId)
    {
        return usersClient.IsAdminAsync(userId);
    }

    public Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser)
    {
        return usersClient.InsertOrUpdateUserAsync(externalUser);
    }

    public Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email)
    {
        return usersClient.AddExternalLinkAsync(userId, provider, externalId, email);
    }
}
