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

/// <summary>Доступ модуля authorization к пользователям; реализация в хосте ходит в модуль users внутри процесса.</summary>
public interface IUsersClient
{
    Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser);

    Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long id);

    Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId);

    Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId);


    /// <summary>При конфликте errorCode = "external_id_taken", conflictOwnerId — ID владельца привязки.</summary>
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email);

    /// <summary>PAT по хэшу секрета. Возвращает и просроченный, и отозванный: пригодность решает вызывающий через
    /// <see cref="PersonalAccessToken.IsUsable"/>, потому что время берётся из его TimeProvider.</summary>
    Task<PersonalAccessToken?> FindPersonalAccessTokenAsync(byte[] tokenHash);

    Task TouchPersonalAccessTokenAsync(long tokenId);
}
