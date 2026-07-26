using Users.BO.Interfaces;
using Users.DA.Interfaces;
using Users.Entities.DbModels.Users;

namespace Users.BO;

public sealed class UserExternalLinksService(
    IUserExternalLinksRepository externalLinksRepository) : IUserExternalLinksService
{
    public Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId)
    {
        return externalLinksRepository.FindUserByProviderAsync(provider, externalId);
    }

    public Task<UserExternalLinkDbModel> AddLinkAsync(long userId, string provider, string externalId, string? email)
    {
        return externalLinksRepository.AddLinkAsync(userId, provider, externalId, email);
    }

    public Task RemoveLinkAsync(long userId, string provider)
    {
        return externalLinksRepository.RemoveLinkAsync(userId, provider);
    }

    public Task<UserExternalLinkDbModel[]> GetLinksAsync(long userId)
    {
        return externalLinksRepository.GetLinksAsync(userId);
    }
}
