using Bugget.Entities.Errors;
using Dapper;
using Npgsql;
using Users.BO.Ports;
using Users.Entities.BO;

namespace Users.DA.DbClients;

public class WorkspaceMembersDbClient : PostgresClient, IWorkspaceMembersRepository
{
    public async Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role, int sizeLimit)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        try
        {
            return (await conn.QuerySingleAsync<WorkspaceMember>(
                "SELECT * FROM create_workspace_member(@user_id, @workspace_id, @role, @size_limit)",
                new { user_id = userId, workspace_id = workspaceId, role = role, size_limit = sizeLimit }
            ), null);
        }
        catch (PostgresException ex)
        {
            if (ex.MessageText == "workspace_limit_exceeded")
            {
                return (null, WorkspaceMembersErrors.WorkspaceLimitExceededError);
            }

            throw;
        }
    }

    public async Task<WorkspaceMember> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        return await conn.QuerySingleAsync<WorkspaceMember>(
            "SELECT * FROM create_workspace_member(@user_id, @workspace_id, @role)",
            new { user_id = userId, workspace_id = workspaceId, role = role }
        );

    }

    public async Task<WorkspaceMember> UpdateWorkspaceMemberAsync(long userId, int workspaceId, string role)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<WorkspaceMember>(
            "SELECT * FROM update_workspace_member(@user_id, @workspace_id, @role)",
            new { user_id = userId, workspace_id = workspaceId, role = role }
        );
    }

    public async Task<WorkspaceMember[]> ListWorkspaceMembersAsync(int workspaceId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<WorkspaceMember>(
            "SELECT * FROM list_workspace_members(@workspace_id)",
            new { workspace_id = workspaceId }
        )).ToArray();
    }

    public async Task DeleteWorkspaceMemberAsync(long userId, int workspaceId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT delete_workspace_member(@user_id, @workspace_id)",
            new { user_id = userId, workspace_id = workspaceId }
        );
    }
}
