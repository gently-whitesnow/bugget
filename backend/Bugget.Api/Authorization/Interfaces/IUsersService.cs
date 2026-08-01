using System.Threading.Tasks;
using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Models;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;

namespace Bugget.Api.Authorization.Interfaces;

public interface IUsersService
{
    Task<UserContext?> GetUserAsync(long id);
    Task<UserContext?> GetUserByExternalIdAsync(string externalId);
    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);
    Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser);
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email);
}
