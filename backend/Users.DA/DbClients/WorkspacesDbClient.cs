using Dapper;
using Users.BO.Ports;
using Users.Entities.BO;

namespace Users.DA.DbClients;

public sealed class WorkspacesDbClient : PostgresClient, IWorkspacesRepository
{
    public async Task<Workspace> CreateWorkspaceAsync(long userId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<Workspace>(
            "SELECT * FROM create_workspace(@user_id, @name)",
            new { user_id = userId, name = name }
        );
    }

    public async Task<Workspace> CreateWorkspaceAsync(string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<Workspace>(
            "SELECT * FROM create_workspace(@name)",
            new { name = name }
        );
    }

    public async Task<Workspace[]> ListWorkspacesAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<Workspace>(
            "SELECT * FROM list_workspaces(@user_id)",
            new { user_id = userId }
        )).ToArray();
    }

    public async Task<Workspace> UpdateWorkspaceAsync(int workspaceId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<Workspace>(
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

    public async Task<Workspace[]> ListWorkspacesAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<Workspace>(
            "SELECT * FROM list_workspaces()"
        )).ToArray();
    }
}
