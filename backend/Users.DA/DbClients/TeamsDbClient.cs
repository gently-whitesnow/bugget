using Bugget.Entities.Errors;
using Dapper;
using Npgsql;
using Users.BO.Ports;
using Users.Entities.BO;

namespace Users.DA.DbClients;

public class TeamsDbClient : PostgresClient, ITeamsRepository
{
    public async Task<Team[]> ListTeamsAsync(int[] workspaceIds)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<Team>(
            "SELECT * FROM list_teams(@workspace_ids)",
            new { workspace_ids = workspaceIds }
        )).ToArray();
    }

    public async Task<Team[]> ListTeamsAsync(int workspaceId, int[] teamIds)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<Team>(
            "SELECT * FROM teams WHERE workspace_id = @workspace_id AND id = ANY(@team_ids)",
            new { workspace_id = workspaceId, team_ids = teamIds }
        )).ToArray();
    }

    public async Task<Team[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<Team>(
            "SELECT * FROM autocomplete_teams(@workspace_id, @search_string, @skip, @take)",
            new { workspace_id = workspaceId, search_string = searchString, skip, take }
        )).ToArray();
    }

    public async Task<Team> CreateTeamAsync(int workspaceId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<Team>(
            "SELECT * FROM create_team(@workspace_id, @name)",
            new { workspace_id = workspaceId, name = name }
        );
    }

    public async Task<(Team? Value, Error? Error)> CreateTeamAsync(int workspaceId, string name, int teamsCountLimit)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        try
        {
            return (await conn.QuerySingleAsync<Team>(
                "SELECT * FROM create_team(@workspace_id, @name, @size_limit)",
                new { workspace_id = workspaceId, name, size_limit = teamsCountLimit }
            ), null);
        }
        catch (PostgresException ex)
        {
            if (ex.MessageText == "teams_count_limit_exceeded")
            {
                return (null, TeamsErrors.TeamsCountLimitExceededError);
            }

            throw;
        }
    }

    public async Task<Team> UpdateTeamAsync(int workspaceId, int teamId, string name)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<Team>(
            "SELECT * FROM update_team(@workspace_id, @team_id, @name)",
            new { workspace_id = workspaceId, team_id = teamId, name = name }
        );
    }

    public async Task DeleteTeamAsync(int workspaceId, int teamId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT delete_team(@workspace_id, @team_id)",
            new { workspace_id = workspaceId, team_id = teamId }
        );
    }
}
