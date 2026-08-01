using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface IUserExternalLinksDbClient
{
    Task<long?> FindUserByProviderAsync(string provider, string externalId);
    Task<UserExternalLink> AddLinkAsync(long userId, string provider, string externalId, string? email);
    Task RemoveLinkAsync(long userId, string provider);
    Task<UserExternalLink[]> GetLinksAsync(long userId);
}
