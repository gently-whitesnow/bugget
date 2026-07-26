using Dapper;
using Users.DA.Interfaces;
using Users.Entities.DbModels.Members;

namespace Users.DA.DbClients;

public class MembersDbClient : PostgresClient, IMembersRepository
{
    public async Task<(WorkspaceMemberDbModel[], TeamMemberDbModel[])> ListMembersAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await using var multi = await conn.QueryMultipleAsync(
            @"
            SELECT * FROM list_workspaces_members(@user_id);
            SELECT * FROM list_teams_members(@user_id);
            ",
            new { user_id = userId }
        );
        var workspaces = (await multi.ReadAsync<WorkspaceMemberDbModel>()).ToArray();
        var teams = (await multi.ReadAsync<TeamMemberDbModel>()).ToArray();
        return (workspaces, teams);
    }
}
