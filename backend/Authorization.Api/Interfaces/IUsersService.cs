using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Models;

namespace Authorization.Api.Interfaces;

public interface IUsersService
{
    Task<UserContext?> GetUserAsync(long id);
    Task<UserContext?> GetUserByExternalIdAsync(string externalId);
    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);
    Task<bool> IsAdminAsync(long userId);
    Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser);
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email);
}
