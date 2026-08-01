using Bugget.Domain.Users;

namespace Bugget.Application.Users.Interfaces;

public interface IUserExternalLinksService
{
    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);
    Task<UserExternalLink> AddLinkAsync(long userId, string provider, string externalId, string? email);
    Task RemoveLinkAsync(long userId, string provider);
    Task<UserExternalLink[]> GetLinksAsync(long userId);
}
