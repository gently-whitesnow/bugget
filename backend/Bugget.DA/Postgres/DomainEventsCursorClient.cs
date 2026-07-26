using System.Data;
using Bugget.DA.Interfaces;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class DomainEventsCursorClient : PostgresClient, IDomainEventsCursorClient
{
    public async Task<long?> GetAsync(string consumerName, CancellationToken ct)
    {
        const string sql = @"
SELECT last_event_id
FROM public.domain_events_cursor
WHERE consumer_name = @consumerName;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
            sql, new { consumerName }, cancellationToken: ct));
    }

    public async Task<bool> TryInitAsync(string consumerName, long initialEventId, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO public.domain_events_cursor (consumer_name, last_event_id)
VALUES (@consumerName, @initialEventId)
ON CONFLICT (consumer_name) DO NOTHING;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            sql, new { consumerName, initialEventId }, cancellationToken: ct));
        return inserted == 1;
    }

    public async Task<int> UpdateAsync(
        string consumerName,
        long newLastEventId,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken ct)
    {
        const string sql = @"
UPDATE public.domain_events_cursor
SET last_event_id = @newLastEventId, updated_at = now()
WHERE consumer_name = @consumerName
  AND last_event_id < @newLastEventId;";

        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { consumerName, newLastEventId },
            transaction: transaction,
            cancellationToken: ct));
    }
}
