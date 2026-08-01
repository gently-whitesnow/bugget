using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Dapper;

namespace Bugget.Infrastructure.Users.DbClients;

public class MembersDbClient : PostgresClient, IMembersRepository
{
    public async Task<(WorkspaceMember[], TeamMember[])> ListMembersAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await using var multi = await conn.QueryMultipleAsync(
            @"
            SELECT * FROM list_workspaces_members(@user_id);
            SELECT * FROM list_teams_members(@user_id);
            ",
            new { user_id = userId }
        );
        var workspaces = (await multi.ReadAsync<WorkspaceMember>()).ToArray();
        var teams = (await multi.ReadAsync<TeamMember>()).ToArray();
        return (workspaces, teams);
    }
}
