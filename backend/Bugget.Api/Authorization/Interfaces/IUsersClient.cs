using System.Threading.Tasks;
using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Models;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.Domain.Errors;
// Полное имя PersonalAccessToken ниже намеренно: короткий using Bugget.Domain.Users
// сталкивает User этого модуля с Bugget.Domain.Users.User.
using PersonalAccessToken = Bugget.Domain.Users.PersonalAccessToken;

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

    /// <summary>
    /// Токен неинтерактивного доступа по хэшу секрета — вход PAT-схемы аутентификации.
    /// Возвращает и просроченный, и отозванный: пригодность решает вызывающий через
    /// <see cref="PersonalAccessToken.IsUsable"/>, потому что время берётся из его
    /// <see cref="System.TimeProvider"/>.
    /// </summary>
    Task<PersonalAccessToken?> FindPersonalAccessTokenAsync(byte[] tokenHash);

    /// <summary>
    /// Отметка об использовании токена после успешной аутентификации.
    /// </summary>
    Task TouchPersonalAccessTokenAsync(long tokenId);
}
