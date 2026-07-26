using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.DomainEvents;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class DomainEventsDbClient : PostgresClient, IDomainEventsDbClient
{
    private const string InsertSql = @"
INSERT INTO public.domain_events
    (workspace_id, aggregate_type, aggregate_id, event_type, event_version,
     payload, actor_user_id, actor_creator_type, correlation_id)
VALUES
    (@workspace_id, @aggregate_type, @aggregate_id, @event_type, @event_version,
     @payload::jsonb, @actor_user_id, @actor_creator_type, @correlation_id)
RETURNING id;";

    private static object BuildInsertParams(DomainEventDbModel evt) => new
    {
        workspace_id = evt.WorkspaceId,
        aggregate_type = evt.AggregateType,
        aggregate_id = evt.AggregateId,
        event_type = evt.EventType,
        event_version = evt.EventVersion,
        payload = evt.Payload,
        actor_user_id = evt.ActorUserId,
        actor_creator_type = evt.ActorCreatorType,
        correlation_id = evt.CorrelationId
    };

    public async Task<long> InsertAsync(DomainEventDbModel evt, ITransactionScope scope, CancellationToken ct = default)
    {
        var (connection, tx) = scope.Unwrap();
        var command = new CommandDefinition(
            InsertSql,
            BuildInsertParams(evt),
            transaction: tx,
            cancellationToken: ct);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task<long> InsertAsync(DomainEventDbModel evt, CancellationToken ct = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(ct);
        var command = new CommandDefinition(
            InsertSql,
            BuildInsertParams(evt),
            cancellationToken: ct);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task<IReadOnlyList<DomainEventDbModel>> ListAsync(
        string workspaceId,
        long sinceId,
        int limit,
        string[]? eventTypes,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT id, workspace_id, aggregate_type, aggregate_id,
       event_type, event_version, payload::text AS payload,
       actor_user_id, actor_creator_type, occurred_at, correlation_id
FROM public.domain_events
WHERE workspace_id = @workspaceId
  AND id > @sinceId
  AND (@eventTypes::text[] IS NULL OR event_type = ANY(@eventTypes::text[]))
ORDER BY id
LIMIT @limit;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<DomainEventRow>(new CommandDefinition(
            sql,
            new
            {
                workspaceId,
                sinceId,
                limit,
                eventTypes = (eventTypes != null && eventTypes.Length > 0) ? eventTypes : null,
            },
            cancellationToken: ct));

        return rows.Select(r => new DomainEventDbModel
        {
            Id = r.Id,
            WorkspaceId = r.WorkspaceId,
            AggregateType = r.AggregateType,
            AggregateId = r.AggregateId,
            EventType = r.EventType,
            EventVersion = r.EventVersion,
            Payload = r.Payload,
            ActorUserId = r.ActorUserId,
            ActorCreatorType = r.ActorCreatorType,
            OccurredAt = r.OccurredAt,
            CorrelationId = r.CorrelationId,
        }).ToArray();
    }

    public async Task<IReadOnlyList<DomainEventDbModel>> ListAllAsync(
        long sinceId,
        int limit,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT id, workspace_id, aggregate_type, aggregate_id,
       event_type, event_version, payload::text AS payload,
       actor_user_id, actor_creator_type, occurred_at, correlation_id
FROM public.domain_events
WHERE id > @sinceId
ORDER BY id
LIMIT @limit;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<DomainEventRow>(new CommandDefinition(
            sql,
            new { sinceId, limit },
            cancellationToken: ct));

        return rows.Select(r => new DomainEventDbModel
        {
            Id = r.Id,
            WorkspaceId = r.WorkspaceId,
            AggregateType = r.AggregateType,
            AggregateId = r.AggregateId,
            EventType = r.EventType,
            EventVersion = r.EventVersion,
            Payload = r.Payload,
            ActorUserId = r.ActorUserId,
            ActorCreatorType = r.ActorCreatorType,
            OccurredAt = r.OccurredAt,
            CorrelationId = r.CorrelationId,
        }).ToArray();
    }

    public async Task<long> GetLatestIdAsync(string workspaceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT COALESCE(MAX(id), 0)
FROM public.domain_events
WHERE workspace_id = @workspaceId;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            sql, new { workspaceId }, cancellationToken: ct));
    }

    public async Task<long> GetLatestIdAcrossAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COALESCE(MAX(id), 0) FROM public.domain_events;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            sql, cancellationToken: ct));
    }

    private sealed class DomainEventRow
    {
        public long Id { get; init; }
        public string WorkspaceId { get; init; } = default!;
        public string AggregateType { get; init; } = default!;
        public string AggregateId { get; init; } = default!;
        public string EventType { get; init; } = default!;
        public short EventVersion { get; init; }
        public string Payload { get; init; } = default!;
        public string? ActorUserId { get; init; }
        public short? ActorCreatorType { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public Guid? CorrelationId { get; init; }
    }
}
