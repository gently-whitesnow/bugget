using Dapper;
using Users.DA.Interfaces;
using Users.Entities.DbModels.Users;

namespace Users.DA.DbClients;

public sealed class UserExternalLinksDbClient : PostgresClient, IUserExternalLinksRepository
{
    public async Task<long?> FindUserByProviderAsync(string provider, string externalId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<long?>(
            "SELECT * FROM find_user_by_provider(@provider, @external_id)",
            new { provider, external_id = externalId }
        );
    }

    public async Task<UserExternalLinkDbModel> AddLinkAsync(long userId, string provider, string externalId, string? email)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<UserExternalLinkDbModel>(
            "SELECT * FROM add_external_link(@user_id, @provider, @external_id, @email)",
            new { user_id = userId, provider, external_id = externalId, email }
        );
    }

    public async Task RemoveLinkAsync(long userId, string provider)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "SELECT remove_external_link(@user_id, @provider)",
            new { user_id = userId, provider }
        );
    }

    public async Task<UserExternalLinkDbModel[]> GetLinksAsync(long userId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<UserExternalLinkDbModel>(
            "SELECT * FROM get_external_links(@user_id)",
            new { user_id = userId }
        )).ToArray();
    }
}
