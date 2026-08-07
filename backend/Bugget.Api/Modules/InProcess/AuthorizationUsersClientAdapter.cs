using Bugget.Api.Authorization;
using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Models;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using UserExternalLinksService = Bugget.Application.Users.Interfaces.IUserExternalLinksService;
using UsersService = Bugget.Application.Users.Interfaces.IUsersService;

namespace Bugget.Api.Modules.InProcess;

/// <summary>
/// Доступ модуля authorization к пользователям. Раньше был HTTP-вызовом в users-api
/// (<c>_internal/users/*</c>), после объединения сервисов — прямой вызов.
/// </summary>
public sealed class AuthorizationUsersClientAdapter(
    UsersService usersService,
    UserExternalLinksService externalLinksService,
    IPersonalAccessTokensDbClient personalAccessTokens) : IUsersClient
{
    public async Task<User> InsertOrUpdateUserAsync(IExternalUser externalUser)
    {
        var created = await usersService.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = externalUser.ExternalId,
            Name = externalUser.Name,
            ImageUrl = externalUser.ImageUrl,
        });

        return MapUser(created);
    }

    public async Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long id)
    {
        var result = await usersService.GetUserContextAsync(id);
        return MapContext(result);
    }

    public async Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId)
    {
        var result = await usersService.GetUserContextByExternalIdAsync(externalId);
        return MapContext(result);
    }

    public Task<long?> FindUserByProviderAndExternalIdAsync(string provider, string externalId) =>
        externalLinksService.FindUserByProviderAndExternalIdAsync(provider, externalId);


    public async Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> AddExternalLinkAsync(
        long userId, string provider, string externalId, string? email)
    {
        // Повторяет контракт InternalUsersController: занятая привязка — конфликт с id владельца.
        var existing = await externalLinksService.FindUserByProviderAndExternalIdAsync(provider, externalId);
        if (existing is not null)
        {
            return (false, "external_id_taken", existing.Value.ToString());
        }

        await externalLinksService.AddLinkAsync(userId, provider, externalId, email);
        return (true, null, null);
    }

    public Task<Bugget.Domain.Users.PersonalAccessToken?> FindPersonalAccessTokenAsync(byte[] tokenHash) =>
        personalAccessTokens.FindByHashAsync(tokenHash);

    public Task TouchPersonalAccessTokenAsync(long tokenId) =>
        personalAccessTokens.TouchLastUsedAsync(tokenId);

    private static (UserContext? Value, Error? Error) MapContext((Bugget.Application.Users.UserContext? Value, Error? Error) result)
    {
        if (result.Error is not null)
        {
            return (null, result.Error);
        }

        var context = result.Value;
        if (context is null)
        {
            return (null, BoErrors.UserNotFound);
        }

        return (new UserContext(
            MapUser(context.User),
            [.. context.Workspaces.Select(w => new WorkspaceMember(w.WorkspaceId, w.Role, w.TeamIds))]), null);
    }

    private static User MapUser(Bugget.Domain.Users.User user) => new()
    {
        Id = user.Id,
        ExternalId = user.ExternalId,
        Name = user.Name,
        ImageUrl = user.ImageUrl,
        RegistrationDate = user.RegistrationDate,
        UpdatedAt = user.UpdatedAt,
    };
}
