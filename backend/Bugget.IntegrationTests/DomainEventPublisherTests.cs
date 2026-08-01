using Bugget.Application.DomainEvents;
using Bugget.Application.Ports;
using Bugget.Domain.DomainEvents;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class DomainEventPublisherTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IDomainEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _connectionString;

    public DomainEventPublisherTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
        _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "PublishAsync в транзакции пишет event после commit")]
    public async Task PublishAsync_WithCommit_PersistsEvent()
    {
        var evt = NewEvent();

        var id = await _unitOfWork.ExecuteAsync(
            (scope, ct) => _publisher.PublishAsync(evt, scope, ct));

        Assert.True(id > 0);

        await using var read = new NpgsqlConnection(_connectionString);
        await read.OpenAsync();
        var count = await read.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.domain_events WHERE aggregate_type=@t AND aggregate_id=@a",
            new { t = evt.AggregateType, a = evt.AggregateId });
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "PublishAsync c rollback не оставляет event")]
    public async Task PublishAsync_WithRollback_DoesNotPersistEvent()
    {
        var evt = NewEvent();

        await Assert.ThrowsAsync<RollbackSentinelException>(async () =>
            await _unitOfWork.ExecuteAsync(async (scope, ct) =>
            {
                await _publisher.PublishAsync(evt, scope, ct);
                throw new RollbackSentinelException();
            }));

        await using var read = new NpgsqlConnection(_connectionString);
        await read.OpenAsync();
        var count = await read.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.domain_events WHERE aggregate_type=@t AND aggregate_id=@a",
            new { t = evt.AggregateType, a = evt.AggregateId });
        Assert.Equal(0, count);
    }

    private static DomainEvent NewEvent() => new()
    {
        WorkspaceId = $"ws_{Guid.NewGuid()}",
        AggregateType = "bug",
        AggregateId = $"{Random.Shared.Next(100_000, 999_999)}",
        EventType = "bugget.bug.created",
        EventVersion = 1,
        Payload = "{\"bugId\":1}",
    };

    private sealed class RollbackSentinelException : Exception;
}
