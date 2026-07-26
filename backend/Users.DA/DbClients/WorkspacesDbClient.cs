using Dapper;
using Users.DA.Interfaces;
using Users.Entities.DbModels.Organizations;
using Users.Entities.DbModels.Workspaces;

namespace Users.DA.DbClients;

public sealed class WorkspacesDbClient : PostgresClient, IWorkspacesRepository
{
    public async Task<WorkspaceDbModel> CreateWorkspaceAsync(long userId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<WorkspaceDbModel>(
            "SELECT * FROM create_workspace(@user_id, @name)",
            new { user_id = userId, name = name }
        );
    }

    public async Task<WorkspaceDbModel> CreateWorkspaceAsync(string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<WorkspaceDbModel>(
            "SELECT * FROM create_workspace(@name)",
            new { name = name }
        );
    }

    public async Task<WorkspaceDbModel[]> ListWorkspacesAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<WorkspaceDbModel>(
            "SELECT * FROM list_workspaces(@user_id)",
            new { user_id = userId }
        )).ToArray();
    }

    public async Task<WorkspaceDbModel> UpdateWorkspaceAsync(int workspaceId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<WorkspaceDbModel>(
            "SELECT * FROM update_workspace(@workspace_id, @name)",
            new { workspace_id = workspaceId, name }
        );
    }

    public async Task DeleteWorkspaceAsync(int workspaceId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT delete_workspace(@workspace_id)",
            new { workspace_id = workspaceId }
        );
    }

    public async Task<WorkspaceDbModel[]> ListWorkspacesAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<WorkspaceDbModel>(
            "SELECT * FROM list_workspaces()"
        )).ToArray();
    }
}
