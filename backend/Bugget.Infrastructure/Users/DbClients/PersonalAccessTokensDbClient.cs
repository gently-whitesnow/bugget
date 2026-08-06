using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Bugget.Infrastructure.Postgres;
using Dapper;

namespace Bugget.Infrastructure.Users.DbClients;

public sealed class PersonalAccessTokensDbClient() : PostgresClient(Constants.PostgresConnectionStringEnv), IPersonalAccessTokensDbClient
{
    public async Task<PersonalAccessToken> CreateAsync(CreatePersonalAccessTokenDto createDto)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<PersonalAccessToken>(
            "SELECT * FROM create_personal_access_token(@user_id, @workspace_id, @team_id, @label, @token_hash, @token_prefix, @expires_at)",
            new
            {
                user_id = createDto.UserId,
                workspace_id = createDto.WorkspaceId,
                team_id = createDto.TeamId,
                label = createDto.Label,
                token_hash = createDto.TokenHash,
                token_prefix = createDto.TokenPrefix,
                expires_at = createDto.ExpiresAt
            }
        );
    }

    public async Task<PersonalAccessToken[]> ListAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<PersonalAccessToken>(
            "SELECT * FROM list_personal_access_tokens(@user_id)",
            new { user_id = userId }
        )).ToArray();
    }

    public async Task<PersonalAccessToken?> FindByHashAsync(byte[] tokenHash)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<PersonalAccessToken>(
            "SELECT * FROM find_personal_access_token_by_hash(@token_hash)",
            new { token_hash = tokenHash }
        );
    }

    public async Task<bool> RevokeAsync(long id, long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<bool>(
            "SELECT * FROM revoke_personal_access_token(@id, @user_id)",
            new { id, user_id = userId }
        );
    }

    public async Task TouchLastUsedAsync(long id)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT touch_personal_access_token_last_used(@id)",
            new { id }
        );
    }
}
