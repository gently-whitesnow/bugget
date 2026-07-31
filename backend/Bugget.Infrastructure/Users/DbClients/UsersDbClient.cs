using Bugget.Application.Users.Ports;
using Bugget.Contracts.Users.Dto.Users;
using Bugget.Domain.Users;
using Dapper;

namespace Bugget.Infrastructure.Users.DbClients;

public class UsersDbClient : PostgresClient, IUsersRepository
{
    public async Task<User> TryInsertUserAsync(CreateUserDto createUserDto)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<User>(
            "SELECT * FROM try_insert_user(@external_id, @name, @image_url)",
            new
            {
                external_id = createUserDto.ExternalId,
                name = createUserDto.Name,
                image_url = createUserDto.ImageUrl
            }
        );
    }

    public async Task<User?> GetUserAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM get_user(@user_id)",
            new { user_id = userId }
        );
    }

    public async Task<User?> GetUserByExternalIdAsync(string externalId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM get_user_by_external_id(@external_id)",
            new { external_id = externalId }
        );
    }

    public async Task DeleteUserAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.QueryAsync(
            "SELECT * FROM delete_user(@user_id)",
            new { user_id = userId }
        );
    }

    public async Task<User[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<User>(
            "SELECT * FROM autocomplete_users(@workspace_id, @search_string, @skip, @take, @team_id)",
            new { workspace_id = workspaceId, search_string = searchString, skip, take, team_id = teamId }
        )).ToArray();
    }

    public async Task<User[]> ListUsersAsync(long[] userIds, int? workspaceId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<User>(
            "SELECT * FROM list_users(@user_ids, @workspace_id)",
            new { user_ids = userIds, workspace_id = workspaceId }
        )).ToArray();
    }

    public async Task UpdateUserImageUrlAsync(long userId, string? imageUrl)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT * FROM update_user_image_url(@user_id, @image_url)",
            new { user_id = userId, image_url = imageUrl }
        );
    }

    public async Task<User> PutUserAsync(long userId, PutUserDto putUserDto)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<User>(
            "SELECT * FROM put_user(@id, @name)",
            new { id = userId, name = putUserDto.Name }
        );
    }

    public async Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT update_mattermost_user_id(@user_id, @mattermost_user_id)",
            new { user_id = userId, mattermost_user_id = mattermostUserId }
        );
    }

    public async Task<bool> CheckUserOwnsWorkspacesAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<bool>(
            "SELECT * FROM check_user_owns_workspaces(@user_id)",
            new { user_id = userId }
        );
    }

    public async Task MergeUsersAsync(long targetUserId, long sourceUserId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT merge_users(@target_user_id, @source_user_id)",
            new { target_user_id = targetUserId, source_user_id = sourceUserId }
        );
    }
}
