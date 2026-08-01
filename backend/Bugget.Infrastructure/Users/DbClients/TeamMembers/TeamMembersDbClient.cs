using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Bugget.Infrastructure.Postgres;
using Dapper;
using Npgsql;

namespace Bugget.Infrastructure.Users.DbClients;

public class TeamMembersDbClient() : PostgresClient(Constants.PostgresConnectionStringEnv), ITeamMembersDbClient
{
    public async Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(long userId, int teamId, int sizeLimit)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        try
        {
            return (await conn.QuerySingleAsync<TeamMember>(
            "SELECT * FROM create_team_member(@user_id, @team_id, @size_limit)",
                new { user_id = userId, team_id = teamId, size_limit = sizeLimit }
            ), null);
        }
        catch (PostgresException ex)
        {
            if (ex.MessageText == "team_limit_exceeded")
            {
                return (null, TeamMembersErrors.TeamLimitExceededError);
            }

            throw;
        }
    }

    public async Task<TeamMember> CreateTeamMemberAsync(long userId, int teamId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        return await conn.QuerySingleAsync<TeamMember>(
        "SELECT * FROM create_team_member(@user_id, @team_id)",
            new { user_id = userId, team_id = teamId }
        );
    }

    public async Task<TeamMember[]> ListTeamMembersAsync(int teamId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<TeamMember>(
            "SELECT * FROM list_team_members(@team_id)",
            new { team_id = teamId }
        )).ToArray();
    }

    public async Task DeleteTeamMemberAsync(long userId, int teamId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT delete_team_member(@user_id, @team_id)",
            new { user_id = userId, team_id = teamId }
        );
    }

    public async Task<TeamMember[]> ListTeamsMemberAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<TeamMember>(
            "SELECT * FROM list_teams_member(@user_id)",
            new { user_id = userId }
        )).ToArray();
    }
}
