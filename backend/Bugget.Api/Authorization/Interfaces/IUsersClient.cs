using System.Threading.Tasks;
using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Models;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.Domain.Errors;

namespace Bugget.Api.Authorization.Interfaces;

/// <summary>
/// Доступ модуля authorization к данным пользователей.
/// Реализация живёт в хосте и ходит в модуль users внутри процесса.
/// </summary>
public interface IUsersClient
{
    Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser);

    Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long id);

    Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId);

    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);


    /// <summary>
    /// Добавить привязку внешнего провайдера к пользователю.
    /// errorCode = "external_id_taken" при конфликте, conflictOwnerId — ID владельца.
    /// </summary>
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email);
}
