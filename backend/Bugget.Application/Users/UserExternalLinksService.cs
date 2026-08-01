using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;

namespace Bugget.Application.Users;

public sealed class UserExternalLinksService(
    IUserExternalLinksDbClient externalLinksDbClient) : IUserExternalLinksService
{
    public Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId)
    {
        return externalLinksDbClient.FindUserByProviderAsync(provider, externalId);
    }

    public Task<UserExternalLink> AddLinkAsync(long userId, string provider, string externalId, string? email)
    {
        return externalLinksDbClient.AddLinkAsync(userId, provider, externalId, email);
    }

    public Task RemoveLinkAsync(long userId, string provider)
    {
        return externalLinksDbClient.RemoveLinkAsync(userId, provider);
    }

    public Task<UserExternalLink[]> GetLinksAsync(long userId)
    {
        return externalLinksDbClient.GetLinksAsync(userId);
    }
}
