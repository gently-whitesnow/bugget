using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;

namespace Bugget.Application.Users;

public sealed class UserExternalLinksService(
    IUserExternalLinksRepository externalLinksRepository) : IUserExternalLinksService
{
    public Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId)
    {
        return externalLinksRepository.FindUserByProviderAsync(provider, externalId);
    }

    public Task<UserExternalLink> AddLinkAsync(long userId, string provider, string externalId, string? email)
    {
        return externalLinksRepository.AddLinkAsync(userId, provider, externalId, email);
    }

    public Task RemoveLinkAsync(long userId, string provider)
    {
        return externalLinksRepository.RemoveLinkAsync(userId, provider);
    }

    public Task<UserExternalLink[]> GetLinksAsync(long userId)
    {
        return externalLinksRepository.GetLinksAsync(userId);
    }
}
