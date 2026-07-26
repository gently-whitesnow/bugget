using Dapper;
using Flow;
using Npgsql;
using Users.DA.DbClients;
using Users.Entities.DbModels.Teams;

namespace Users.DA.TeamInvites;

public class TeamInvitesDbClient : PostgresClient, ITeamInvitesRepository
{
    public async Task<TeamInviteDbModel> CreateTeamInviteAsync(
        int workspaceId,
         int teamId,
          byte[] tokenHash,
           DateTimeOffset expiresAt)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<TeamInviteDbModel>(
            "SELECT * FROM create_team_invite(@workspace_id, @team_id, @token_hash, @expires_at)",
            new { workspace_id = workspaceId, team_id = teamId, token_hash = tokenHash, expires_at = expiresAt }
        );

    }

    public async Task DeleteTeamInviteAsync(int teamId, int id)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT delete_team_invite(@team_id, @id)",
            new { team_id = teamId, id }
        );
    }

    public async Task<TeamInviteDbModel?> GetTeamInviteAsync(int teamId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<TeamInviteDbModel>(
            "SELECT * FROM get_team_invite(@team_id)",
            new { team_id = teamId }
        );
    }

    public async Task<TeamInviteDbModel?> UpdateTeamInviteAsync(int teamId, int id, byte[] tokenHash, DateTimeOffset expiresAt)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        return await conn.QuerySingleOrDefaultAsync<TeamInviteDbModel>(
            "SELECT * FROM update_team_invite(@team_id, @id, @token_hash, @expires_at)",
            new { team_id = teamId, id, token_hash = tokenHash, expires_at = expiresAt }
        );

    }

    public async Task<TeamInviteDbModel?> AcceptTeamInviteAsync(byte[] tokenHash)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        return await conn.QuerySingleOrDefaultAsync<TeamInviteDbModel>(
            "SELECT * FROM accept_team_invite(@token_hash)",
            new { token_hash = tokenHash }
        );
    }
}
