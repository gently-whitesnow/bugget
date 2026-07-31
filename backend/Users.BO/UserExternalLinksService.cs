using Users.BO.Interfaces;
using Users.BO.Ports;
using Users.Entities.BO;

namespace Users.BO;

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
