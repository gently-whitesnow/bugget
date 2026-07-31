using Users.Entities.BO;

namespace Users.BO.Ports;

public interface IUserExternalLinksRepository
{
    Task<long?> FindUserByProviderAsync(string provider, string externalId);
    Task<UserExternalLink> AddLinkAsync(long userId, string provider, string externalId, string? email);
    Task RemoveLinkAsync(long userId, string provider);
    Task<UserExternalLink[]> GetLinksAsync(long userId);
}
