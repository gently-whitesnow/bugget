using Bugget.BO.Ports;
using Bugget.Entities.BO.DomainEvents;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class DomainEventsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IDomainEventsDbClient _client;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _connectionString;

    public DomainEventsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _client = scope.ServiceProvider.GetRequiredService<IDomainEventsDbClient>();
        _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "InsertAsync пишет event в транзакции, select by aggregate его возвращает")]
    public async Task InsertAsync_WritesEventInsideTransaction_AndSelectByAggregateReturnsIt()
    {
        // Arrange
        var evt = new DomainEvent
        {
            WorkspaceId = $"ws_{Guid.NewGuid()}",
            AggregateType = "bug",
            AggregateId = $"{Random.Shared.Next(100_000, 999_999)}",
            EventType = "bugget.bug.created",
            EventVersion = 1,
            Payload = "{\"bugId\":42,\"title\":\"t\"}",
            ActorUserId = "user_1",
            ActorCreatorType = 0,
            CorrelationId = Guid.NewGuid(),
        };

        // Act — commit path
        var insertedId = await _unitOfWork.ExecuteAsync(
            (scope, ct) => _client.InsertAsync(evt, scope, ct));

        // Assert — row visible via select by aggregate
        Assert.True(insertedId > 0);

        await using var readConn = new NpgsqlConnection(_connectionString);
        await readConn.OpenAsync();

        var rows = (await readConn.QueryAsync<DomainEventRow>(
            @"SELECT id, workspace_id AS WorkspaceId, aggregate_type AS AggregateType, aggregate_id AS AggregateId,
                     event_type AS EventType, event_version AS EventVersion, payload::text AS Payload,
                     actor_user_id AS ActorUserId, actor_creator_type AS ActorCreatorType, correlation_id AS CorrelationId
              FROM public.domain_events
              WHERE aggregate_type = @t AND aggregate_id = @a",
            new { t = evt.AggregateType, a = evt.AggregateId })).ToList();

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(insertedId, row.Id);
        Assert.Equal(evt.WorkspaceId, row.WorkspaceId);
        Assert.Equal(evt.EventType, row.EventType);
        Assert.Equal(evt.EventVersion, row.EventVersion);
        Assert.Equal(evt.ActorUserId, row.ActorUserId);
        Assert.Equal(evt.ActorCreatorType, row.ActorCreatorType);
        Assert.Equal(evt.CorrelationId, row.CorrelationId);
        Assert.Contains("\"bugId\"", row.Payload);
    }

    [Fact(DisplayName = "Rollback транзакции не оставляет event в таблице")]
    public async Task InsertAsync_WithRollback_DoesNotPersistEvent()
    {
        // Arrange
        var evt = new DomainEvent
        {
            WorkspaceId = $"ws_{Guid.NewGuid()}",
            AggregateType = "bug",
            AggregateId = $"{Random.Shared.Next(100_000, 999_999)}",
            EventType = "bugget.bug.created",
            EventVersion = 1,
            Payload = "{\"bugId\":1}",
        };

        // Act — throw inside UoW action triggers tx dispose without commit (rollback)
        await Assert.ThrowsAsync<RollbackSentinelException>(async () =>
            await _unitOfWork.ExecuteAsync(async (scope, ct) =>
            {
                await _client.InsertAsync(evt, scope, ct);
                throw new RollbackSentinelException();
            }));

        // Assert
        await using var readConn = new NpgsqlConnection(_connectionString);
        await readConn.OpenAsync();
        var count = await readConn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.domain_events WHERE aggregate_type=@t AND aggregate_id=@a",
            new { t = evt.AggregateType, a = evt.AggregateId });
        Assert.Equal(0, count);
    }

    private sealed record DomainEventRow(
        long Id,
        string WorkspaceId,
        string AggregateType,
        string AggregateId,
        string EventType,
        short EventVersion,
        string Payload,
        string? ActorUserId,
        short? ActorCreatorType,
        Guid? CorrelationId);

    private sealed class RollbackSentinelException : Exception;
}
