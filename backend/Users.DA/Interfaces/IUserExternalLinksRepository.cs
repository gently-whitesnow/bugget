using Users.Entities.DbModels.Users;

namespace Users.DA.Interfaces;

public interface IUserExternalLinksRepository
{
    Task<long?> FindUserByProviderAsync(string provider, string externalId);
    Task<UserExternalLinkDbModel> AddLinkAsync(long userId, string provider, string externalId, string? email);
    Task RemoveLinkAsync(long userId, string provider);
    Task<UserExternalLinkDbModel[]> GetLinksAsync(long userId);
}
