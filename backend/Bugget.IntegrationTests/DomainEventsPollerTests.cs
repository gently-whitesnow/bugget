using Bugget.BO.DomainEvents;
using Bugget.BO.DomainEvents.Consumer;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.DomainEvents;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Smoke-тесты <see cref="DomainEventsPoller"/> через реальный Postgres
/// (Testcontainers): миграция применилась, cursor продвигается после успешного
/// handler-вызова и остаётся на месте при исключении handler'а. Вызываем
/// <see cref="DomainEventsPoller.TickAsync"/> напрямую, чтобы избежать гонок с
/// фоновой сборкой Web App'а.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DomainEventsPollerTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IDomainEventsConsumerRuntime _runtime;
    private readonly IDomainEventsCursorClient _cursorClient;
    private readonly IDomainEventsDbClient _eventsClient;
    private readonly IUnitOfWork _uow;
    private readonly string _connectionString;

    public DomainEventsPollerTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _runtime = scope.ServiceProvider.GetRequiredService<IDomainEventsConsumerRuntime>();
        _cursorClient = scope.ServiceProvider.GetRequiredService<IDomainEventsCursorClient>();
        _eventsClient = scope.ServiceProvider.GetRequiredService<IDomainEventsDbClient>();
        _uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "Миграция 040 создала domain_events_cursor")]
    public async Task Migration_Created_Cursor_Table()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var exists = await conn.ExecuteScalarAsync<bool>(@"
SELECT EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema='public' AND table_name='domain_events_cursor');");
        Assert.True(exists);
    }

    [Fact(DisplayName = "Poller продвигает cursor после успешного no-op handler'а")]
    public async Task Poller_AdvancesCursor_After_NoOp_Handler()
    {
        // Arrange — уникальный consumer на тест, чтобы не пересекаться с seed'ом миграции
        var consumerName = $"test_advance_{Guid.NewGuid():N}";
        await EnsureConsumerStartsFromZero(consumerName);

        // вставляем 2 события через UoW (тот же путь, что и в проде)
        var id1 = await InsertEventAsync(BuggetEventTypes.ReportStatusChanged, payload: "{}");
        var id2 = await InsertEventAsync(BuggetEventTypes.ReportStatusChanged, payload: "{}");

        var handler = new CountingHandler(BuggetEventTypes.ReportStatusChanged);
        var poller = BuildPoller(consumerName, [handler]);

        // Act — два тика, потому что после первого тика cursor продвинется только
        // на конкретно эти события (тестовая БД может содержать seed-события).
        await poller.TickAsync(CancellationToken.None);

        // Assert
        Assert.Contains(id1, handler.HandledIds);
        Assert.Contains(id2, handler.HandledIds);
        var cursorNow = await _cursorClient.GetAsync(consumerName, CancellationToken.None);
        Assert.NotNull(cursorNow);
        Assert.True(cursorNow.Value >= id2);
    }

    [Fact(DisplayName = "Handler exception: cursor НЕ продвигается")]
    public async Task Poller_DoesNotAdvanceCursor_OnHandlerException()
    {
        var consumerName = $"test_fail_{Guid.NewGuid():N}";
        await EnsureConsumerStartsFromZero(consumerName);
        var cursorBefore = (await _cursorClient.GetAsync(consumerName, CancellationToken.None))!.Value;

        var poisonId = await InsertEventAsync(BuggetEventTypes.ReportStatusChanged, payload: "{}");

        var handler = new ThrowingHandler(BuggetEventTypes.ReportStatusChanged);
        var poller = BuildPoller(consumerName, [handler]);

        await poller.TickAsync(CancellationToken.None);

        var cursorAfter = (await _cursorClient.GetAsync(consumerName, CancellationToken.None))!.Value;
        // cursor может быть продвинут на события ДО poison-pill (если они были), но не на сам poison-pill
        Assert.True(cursorAfter < poisonId,
            $"Cursor должен быть строго меньше id отравленного события: cursor={cursorAfter} poisonId={poisonId}");
        Assert.True(cursorAfter >= cursorBefore);
    }

    [Fact(DisplayName = "Неизвестный event_type: cursor продвигается (skip-unknown семантика)")]
    public async Task Poller_AdvancesCursor_When_EventType_Is_Unknown()
    {
        var consumerName = $"test_unknown_{Guid.NewGuid():N}";
        await EnsureConsumerStartsFromZero(consumerName);

        var id1 = await InsertEventAsync("bugget.totally.unknown.event", payload: "{}");

        // Нет ни одного handler'а — все события пропускаются как «no handler», cursor двигается.
        var poller = BuildPoller(consumerName, []);

        await poller.TickAsync(CancellationToken.None);

        var cursorNow = await _cursorClient.GetAsync(consumerName, CancellationToken.None);
        Assert.NotNull(cursorNow);
        Assert.True(cursorNow.Value >= id1);
    }

    // --- helpers ---

    private async Task EnsureConsumerStartsFromZero(string consumerName)
    {
        // Bootstrap cursor от 0 — для теста нам нужно «увидеть» только что вставленные события.
        // (Логика prod-bootstrap'а живёт в seed'е миграции 040; тут мы forсируем 0,
        // чтобы тест был детерминирован.)
        await _cursorClient.TryInitAsync(consumerName, 0, CancellationToken.None);
    }

    private async Task<long> InsertEventAsync(string eventType, string payload)
    {
        return await _uow.ExecuteAsync(async (scope, ct) =>
        {
            var evt = new DomainEventDbModel
            {
                WorkspaceId = "ws_test",
                AggregateType = "report",
                AggregateId = $"{Random.Shared.Next(100_000, 999_999)}",
                EventType = eventType,
                EventVersion = 1,
                Payload = payload,
            };
            return await _eventsClient.InsertAsync(evt, scope, ct);
        });
    }

    private DomainEventsPoller BuildPoller(string consumerName, IEnumerable<IDomainEventHandler> handlers)
    {
        var options = Options.Create(new DomainEventsConsumerOptions
        {
            ConsumerName = consumerName,
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(10),
            ErrorBackoff = TimeSpan.FromMilliseconds(10),
        });
        return new DomainEventsPoller(
            _runtime,
            _cursorClient,
            _eventsClient,
            handlers,
            options,
            NullLogger<DomainEventsPoller>.Instance,
            TimeProvider.System);
    }

    private sealed class CountingHandler(string eventType) : IDomainEventHandler
    {
        public string EventType { get; } = eventType;
        public List<long> HandledIds { get; } = [];

        public Task HandleAsync(
            DomainEventDbModel evt,
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            CancellationToken ct)
        {
            HandledIds.Add(evt.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(string eventType) : IDomainEventHandler
    {
        public string EventType { get; } = eventType;

        public Task HandleAsync(
            DomainEventDbModel evt,
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            CancellationToken ct)
        {
            throw new InvalidOperationException("intentional failure");
        }
    }
}
