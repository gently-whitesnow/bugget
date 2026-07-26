using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Models;
using Flow;

namespace Authorization.Api.Interfaces;

/// <summary>
/// Доступ модуля authorization к данным пользователей.
/// Реализация живёт в хосте и ходит в модуль users внутри процесса.
/// </summary>
public interface IUsersClient
{
    Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser);

    Task<ResultStruct<UserContext>> GetUserContextAsync(long id);

    Task<ResultStruct<UserContext>> GetUserContextByExternalIdAsync(string externalId);

    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);

    Task<bool> IsAdminAsync(long userId);

    /// <summary>
    /// Добавить привязку внешнего провайдера к пользователю.
    /// errorCode = "external_id_taken" при конфликте, conflictOwnerId — ID владельца.
    /// </summary>
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email);
}
