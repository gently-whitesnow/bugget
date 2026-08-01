using Bugget.Application.Ports;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Application.Users;

public sealed class UsersService(
    IUsersRepository usersDbClient,
    IMembersRepository membersDbClient,
    ITeamsService teamsService,
    ITaskQueue taskQueue,
    IAvatarDownloadService avatarService) : IUsersService
{
    public async Task<User> TryInsertUserAsync(CreateUserDto createUserDto)
    {
        if (string.IsNullOrEmpty(createUserDto.Name))
        {
            createUserDto.Name = $"пользователь_{createUserDto.ExternalId.Substring(0, 5)}";
        }

        var user = await usersDbClient.TryInsertUserAsync(createUserDto);

        // Запуск фоновой загрузки аватара
        if (!string.IsNullOrEmpty(createUserDto.ImageUrl))
        {
            var imageUrl = createUserDto.ImageUrl;
            var userId = user.Id;

            await taskQueue.EnqueueAsync(async (sp, ct) =>
            {
                await avatarService.DownloadAndSaveAvatarAsync(userId, imageUrl, ct);
            });
        }

        return user;
    }

    public async Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long userId)
    {
        var user = await usersDbClient.GetUserAsync(userId);
        if (user is null)
        {
            return (null, BoErrors.NotFoundError);
        }

        var (workspacesMember, _) = await membersDbClient.ListMembersAsync(user.Id);

        var teams = await teamsService.ListTeamsAsync(workspacesMember.Select(w => w.WorkspaceId).ToArray());

        var workspacesContext = workspacesMember.Select(w => new WorkspaceContext(
            w.WorkspaceId,
            w.Role,
            teams.Where(t => t.WorkspaceId == w.WorkspaceId).Select(t => t.Id).ToArray()
        )).ToArray();

        return (new UserContext(user, workspacesContext), null);
    }

    public async Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId)
    {
        var user = await usersDbClient.GetUserByExternalIdAsync(externalId);
        if (user is null)
        {
            return (null, BoErrors.NotFoundError);
        }

        var (workspacesMember, _) = await membersDbClient.ListMembersAsync(user.Id);

        var teams = await teamsService.ListTeamsAsync(workspacesMember.Select(w => w.WorkspaceId).ToArray());

        var workspacesContext = workspacesMember.Select(w => new WorkspaceContext(
            w.WorkspaceId,
            w.Role,
            teams.Where(t => t.WorkspaceId == w.WorkspaceId).Select(t => t.Id).ToArray()
        )).ToArray();

        return (new UserContext(user, workspacesContext), null);
    }

    public Task<User?> GetUserAsync(long userId)
    {
        return usersDbClient.GetUserAsync(userId);
    }

    public Task<User[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null)
    {
        return usersDbClient.AutocompleteUsersAsync(workspaceId, searchString, skip, take, teamId);
    }

    public Task<User[]> ListUsersAsync(long[] userIds, int? workspaceId)
    {
        return usersDbClient.ListUsersAsync(userIds, workspaceId);
    }

    public Task DeleteUserAsync(long userId)
    {
        return usersDbClient.DeleteUserAsync(userId);
    }

    public Task<User> PutUserAsync(long userId, PutUserDto putUserDto)
    {
        return usersDbClient.PutUserAsync(userId, putUserDto);
    }

    public Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId)
    {
        return usersDbClient.UpdateMattermostUserIdAsync(userId, mattermostUserId);
    }

    public async Task<(bool Success, string? ErrorCode)> MergeUsersAsync(long targetUserId, long sourceUserId)
    {
        var sourceUser = await usersDbClient.GetUserAsync(sourceUserId);
        if (sourceUser is null)
        {
            return (false, "source_not_found");
        }

        var ownsWorkspaces = await usersDbClient.CheckUserOwnsWorkspacesAsync(sourceUserId);
        if (ownsWorkspaces)
        {
            return (false, "source_owns_workspaces");
        }

        await usersDbClient.MergeUsersAsync(targetUserId, sourceUserId);
        return (true, null);
    }
}
