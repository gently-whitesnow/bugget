using Bugget.DA.Interfaces;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class ParticipantsDbClient : PostgresClient, IParticipantsDbClient
{
    public async Task<string[]?> AddParticipantIfNotExistAsync(int reportId, string userId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var participants = await connection.ExecuteScalarAsync<string[]?>(
            "SELECT public.add_participant_if_not_exist_internal(@report_id, @user_id);",
            new { report_id = reportId, user_id = userId }
        );

        return participants;
    }
}
