using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.DomainEvents.Consumer;
using Bugget.BO.DomainEvents.Handlers;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.DomainEvents;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Integration-тесты <see cref="ReportPhaseProjectionHandler"/>: события вставляются
/// в <c>domain_events</c> через UoW (тот же путь, что и прод-эмиссия), затем гоняем
/// <see cref="DomainEventsPoller.TickAsync"/> и читаем <c>report_phase_intervals</c>.
/// Покрывает контракт handler'а end-to-end через реальный poller.
/// </summary>
[Collection("PostgresCollection")]
public sealed class ReportPhaseProjectionHandlerTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly AppWithPostgresFixture _fixture;
    private readonly IDomainEventsConsumerRuntime _runtime;
    private readonly IDomainEventsCursorClient _cursorClient;
    private readonly IDomainEventsDbClient _eventsClient;
    private readonly IReportPhaseIntervalsDbClient _intervalsClient;
    private readonly IReportsDbClient _reportsDbClient;
    private readonly IUnitOfWork _uow;
    private readonly string _connectionString;

    public ReportPhaseProjectionHandlerTests(AppWithPostgresFixture fixture)
    {
        _fixture = fixture;
        using var scope = fixture.Services.CreateScope();
        _runtime = scope.ServiceProvider.GetRequiredService<IDomainEventsConsumerRuntime>();
        _cursorClient = scope.ServiceProvider.GetRequiredService<IDomainEventsCursorClient>();
        _eventsClient = scope.ServiceProvider.GetRequiredService<IDomainEventsDbClient>();
        _intervalsClient = scope.ServiceProvider.GetRequiredService<IReportPhaseIntervalsDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
        _uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    // ============ Сценарии переходов ============

    [Fact(DisplayName = "Backlog → Fix → Resolved: один Fix (idx=0), Test отсутствует")]
    public async Task Backlog_Fix_Resolved()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Resolved, baseTime.AddMinutes(10)));

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Single(rows);
        Assert.Equal((short)ReportStatus.Fix, rows[0].phase);
        Assert.Equal(0, rows[0].regression_cycle_index);
        Assert.NotNull(rows[0].exited_at);
    }

    [Fact(DisplayName = "Backlog → Fix → Test → Resolved: Fix {0}, Test {0} (без регрессий)")]
    public async Task Backlog_Fix_Test_Resolved()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Test, baseTime.AddMinutes(2)),
            (ReportStatus.Test, ReportStatus.Resolved, baseTime.AddMinutes(10)));

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Equal(2, rows.Count);

        var fixes = rows.Where(r => r.phase == (short)ReportStatus.Fix).ToList();
        Assert.Single(fixes);
        Assert.Equal(0, fixes[0].regression_cycle_index);

        var tests = rows.Where(r => r.phase == (short)ReportStatus.Test).ToList();
        Assert.Single(tests);
        Assert.Equal(0, tests[0].regression_cycle_index);
        Assert.All(rows, r => Assert.NotNull(r.exited_at));
    }

    [Fact(DisplayName = "Backlog → Fix → Test → Fix → Test → Resolved: Fix {0,1}, Test {0,1} (одна регрессия)")]
    public async Task Backlog_Fix_Test_Fix_Test_Resolved()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Test, baseTime.AddMinutes(2)),
            (ReportStatus.Test, ReportStatus.Fix, baseTime.AddMinutes(3)),
            (ReportStatus.Fix, ReportStatus.Test, baseTime.AddMinutes(4)),
            (ReportStatus.Test, ReportStatus.Resolved, baseTime.AddMinutes(10)));

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Equal(4, rows.Count);

        var fixes = rows.Where(r => r.phase == (short)ReportStatus.Fix)
            .OrderBy(r => r.regression_cycle_index).ToList();
        Assert.Equal(new[] { 0, 1 }, fixes.Select(r => r.regression_cycle_index).ToArray());

        var tests = rows.Where(r => r.phase == (short)ReportStatus.Test)
            .OrderBy(r => r.regression_cycle_index).ToList();
        Assert.Equal(new[] { 0, 1 }, tests.Select(r => r.regression_cycle_index).ToArray());
        Assert.All(rows, r => Assert.NotNull(r.exited_at));
    }

    [Fact(DisplayName = "Backlog → Fix → Rejected: один Fix, Test отсутствует")]
    public async Task Backlog_Fix_Rejected()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Rejected, baseTime.AddMinutes(5)));

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Single(rows);
        Assert.Equal((short)ReportStatus.Fix, rows[0].phase);
        Assert.Equal(0, rows[0].regression_cycle_index);
        Assert.NotNull(rows[0].exited_at);
    }

    [Fact(DisplayName = "Backlog → Resolved: manual close без активных фаз — read-model пустая")]
    public async Task Backlog_Resolved_NoIntervals()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Resolved, baseTime.AddMinutes(1)));

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Empty(rows);
    }

    [Fact(DisplayName = "Backlog → Fix → Backlog (manual) → Fix → Resolved: второй Fix имеет regression_cycle_index=1")]
    public async Task Backlog_Fix_Backlog_Fix_Resolved()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Backlog, baseTime.AddMinutes(2)),
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(3)),
            (ReportStatus.Fix, ReportStatus.Resolved, baseTime.AddMinutes(10)));

        var rows = await ReadIntervalsAsync(reportId);
        var fixes = rows.Where(r => r.phase == (short)ReportStatus.Fix)
            .OrderBy(r => r.entered_at).ToList();
        Assert.Equal(2, fixes.Count);
        Assert.Equal(0, fixes[0].regression_cycle_index);
        Assert.Equal(1, fixes[1].regression_cycle_index);
        Assert.All(fixes, r => Assert.NotNull(r.exited_at));
    }

    [Fact(DisplayName = "Идемпотентность: повторный poller-tick на тех же событиях ничего не дублирует")]
    public async Task Idempotent_Reprocessing()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        var consumerName = $"test_idem_{Guid.NewGuid():N}";
        var cursorStart = await StartConsumerFromTailAsync(consumerName);

        await InsertStatusEventAsync(reportId, ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1));
        await InsertStatusEventAsync(reportId, ReportStatus.Fix, ReportStatus.Test, baseTime.AddMinutes(2));
        await InsertStatusEventAsync(reportId, ReportStatus.Test, ReportStatus.Fix, baseTime.AddMinutes(3));

        var poller = BuildPoller(consumerName, [BuildHandler()]);
        await poller.TickAsync(CancellationToken.None);

        var firstSnapshot = await ReadIntervalsAsync(reportId);
        // Три строки: Fix#1 закрыт, Test#1 закрыт, Fix#2 активный.
        Assert.Equal(3, firstSnapshot.Count);
        Assert.Single(firstSnapshot, r => r.exited_at is null);

        // «Откатываем» cursor назад на тот же tail и гоняем повторно — handler должен пройти
        // по тем же событиям, UNIQUE(source_event_id) на INSERT и idempotent UPDATE не должны
        // породить новых строк.
        await ResetCursorAsync(consumerName, cursorStart);
        await poller.TickAsync(CancellationToken.None);

        var secondSnapshot = await ReadIntervalsAsync(reportId);
        Assert.Equal(firstSnapshot.Count, secondSnapshot.Count);
        // Состояние строк идентичное (включая source_event_id).
        Assert.Equal(
            firstSnapshot.Select(r => (r.phase, r.regression_cycle_index, r.entered_at, r.exited_at, r.source_event_id)).ToArray(),
            secondSnapshot.Select(r => (r.phase, r.regression_cycle_index, r.entered_at, r.exited_at, r.source_event_id)).ToArray());
    }

    [Fact(DisplayName = "ReportExcludedFromAnalyticsToggled handler НЕ обрабатывает: read-model не меняется")]
    public async Task ExcludedFromAnalytics_NotHandled()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        var consumerName = $"test_excluded_{Guid.NewGuid():N}";
        await StartConsumerFromTailAsync(consumerName);

        // Мешаем «правильное» событие и «игнорируемое» — handler должен среагировать только на первое.
        await InsertStatusEventAsync(reportId, ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1));
        await InsertRawEventAsync(
            reportId,
            BuggetEventTypes.ReportExcludedFromAnalyticsToggled,
            payload: "{\"is_excluded\": true}",
            occurredAt: baseTime.AddMinutes(2));

        // Spy-handler оборачивает основной и проверяет, что для excluded-события HandleAsync не вызвался.
        var spy = new SpyHandler(BuildHandler());
        var poller = BuildPoller(consumerName, [spy]);

        await poller.TickAsync(CancellationToken.None);

        // Poller диспатчит handler'ы по EventType (ToLookup), поэтому SpyHandler с
        // EventType=ReportStatusChanged не получит вызов для ReportExcludedFromAnalyticsToggled.
        Assert.Single(spy.HandledEventTypes);
        Assert.Equal(BuggetEventTypes.ReportStatusChanged, spy.HandledEventTypes[0]);

        var rows = await ReadIntervalsAsync(reportId);
        Assert.Single(rows); // один открытый Fix — от первого события
        Assert.Equal((short)ReportStatus.Fix, rows[0].phase);
        Assert.Null(rows[0].exited_at);
    }

    [Fact(DisplayName = "Активный интервал ровно один на репорт (partial unique index в действии)")]
    public async Task AtMostOneActiveInterval()
    {
        var (reportId, baseTime) = await PrepareReportAsync();

        await DriveAsync(reportId,
            (ReportStatus.Backlog, ReportStatus.Fix, baseTime.AddMinutes(1)),
            (ReportStatus.Fix, ReportStatus.Test, baseTime.AddMinutes(2)));

        var actives = (await ReadIntervalsAsync(reportId)).Where(r => r.exited_at is null).ToList();
        Assert.Single(actives);
        Assert.Equal((short)ReportStatus.Test, actives[0].phase);
    }

    // ============ Smoke через ReportsService → poller → projection ============

    [Fact(DisplayName = "Smoke: PATCH /reports → ReportStatusChanged → poller → report_phase_intervals содержит Test")]
    public async Task EndToEnd_PatchReports_To_Projection()
    {
        // Готовим репорт + workspace.
        var userId = $"user_{Guid.NewGuid()}";
        var orgId = $"org_{Guid.NewGuid()}";
        var report = await _reportsDbClient.CreateReportAsync(userId, null, orgId, new ReportCreateDto { Title = "r-smoke" });

        var consumerName = $"test_smoke_{Guid.NewGuid():N}";
        await StartConsumerFromTailAsync(consumerName);

        // PATCH через ReportsService — у репорта responsibleUserId=null и status=Backlog,
        // PATCH с ResponsibleUserId меняет на Test и эмитит событие.
        var reportsService = _fixture.Services.GetRequiredService<Bugget.BO.Services.Reports.ReportsService>();
        var user = CreateUser(userId, orgId);
        // Manual override на Test (создаём событие Backlog→Test). Используем явный Status,
        // чтобы не зависеть от внутренностей auto-driver'а: CreateReportAsync проставляет
        // responsible_user_id = creator, поэтому driver на смену responsible не сработал бы.
        var result = await reportsService.PatchReportAsync(
            report.Id.ToString(),
            user,
            new ReportPatchDto { Status = (int)ReportStatus.Test });
        Assert.True(result.IsSuccess, $"PATCH не прошёл: {result.Error}");

        // Sanity: статус действительно перешёл в Test.
        Assert.NotNull(result.Value);
        Assert.Equal((int)ReportStatus.Test, result.Value!.Status);

        // Убедимся, что событие действительно эмитнулось в outbox.
        var emitted = await ReadEmittedStatusEventsAsync(report.Id);
        Assert.True(emitted.Count > 0, $"PATCH succeeded но в outbox нет события: report.Id={report.Id}");
        Assert.Single(emitted);
        Assert.Equal(BuggetEventTypes.ReportStatusChanged, emitted[0].event_type);

        // Поднимаем poller — он должен подхватить событие и записать interval.
        var poller = BuildPoller(consumerName, [BuildHandler()]);
        await poller.TickAsync(CancellationToken.None);

        var rows = await ReadIntervalsAsync(report.Id);
        Assert.Single(rows);
        Assert.Equal((short)ReportStatus.Test, rows[0].phase);
        Assert.Equal(0, rows[0].regression_cycle_index);
        Assert.Null(rows[0].exited_at);
    }

    private async Task<List<(long id, string event_type, string payload)>> ReadEmittedStatusEventsAsync(int reportId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<(long id, string event_type, string payload)>(
            @"SELECT id, event_type, payload::text AS payload
              FROM public.domain_events
              WHERE aggregate_type = 'report' AND aggregate_id = @id
              ORDER BY id",
            new { id = reportId.ToString() });
        return rows.ToList();
    }

    // ============ helpers ============

    private static UserIdentity CreateUser(string userId, string organizationId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userId) }, "test");
        identity.AddClaim(new Claim(AuthClaims.OrganizationId, organizationId));
        return new UserIdentity(new ClaimsPrincipal(identity));
    }


    /// <summary>
    /// Создаёт «реальный» репорт через DA, чтобы FK <c>report_phase_intervals.report_id</c>
    /// был валиден. Возвращает <c>(reportId, baseTime)</c>: baseTime используется как
    /// относительный нулевой момент для последующих <c>occurred_at</c>.
    /// </summary>
    private async Task<(int reportId, DateTimeOffset baseTime)> PrepareReportAsync()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var orgId = $"org_{Guid.NewGuid()}";
        var report = await _reportsDbClient.CreateReportAsync(userId, null, orgId, new ReportCreateDto { Title = "r-test" });
        return (report.Id, DateTimeOffset.UtcNow);
    }

    /// <summary>Гоняет цепочку переходов через poller'а: вставляет события и tick'ает.</summary>
    private async Task DriveAsync(int reportId, params (ReportStatus from, ReportStatus to, DateTimeOffset at)[] transitions)
    {
        var consumerName = $"test_{Guid.NewGuid():N}";
        // Start от tail: коллекция тестов гоняется последовательно, к моменту запуска
        // в `domain_events` уже могут быть события прошлых тестов — на них спотыкаться не должны.
        await StartConsumerFromTailAsync(consumerName);

        foreach (var (from, to, at) in transitions)
        {
            await InsertStatusEventAsync(reportId, from, to, at);
        }

        var poller = BuildPoller(consumerName, [BuildHandler()]);
        await poller.TickAsync(CancellationToken.None);
    }

    /// <summary>
    /// Инициализирует cursor консьюмера на текущий MAX(id) из <c>domain_events</c>,
    /// чтобы тест не подхватывал «хвост» от прошлых тестов в shared-collection.
    /// Возвращает позицию-tail (полезно для теста идемпотентности — он сбрасывает cursor сюда).
    /// </summary>
    private async Task<long> StartConsumerFromTailAsync(string consumerName)
    {
        var tail = await _eventsClient.GetLatestIdAcrossAllAsync(CancellationToken.None);
        await _cursorClient.TryInitAsync(consumerName, tail, CancellationToken.None);
        await ResetCursorAsync(consumerName, tail);
        return tail;
    }

    private async Task ResetCursorAsync(string consumerName, long position)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "UPDATE public.domain_events_cursor SET last_event_id = @p WHERE consumer_name = @c",
            new { p = position, c = consumerName });
    }

    private async Task<long> InsertStatusEventAsync(int reportId, ReportStatus from, ReportStatus to, DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            from_status = from.ToString(),
            to_status = to.ToString(),
        });
        return await InsertRawEventAsync(reportId, BuggetEventTypes.ReportStatusChanged, payload, occurredAt);
    }

    private async Task<long> InsertRawEventAsync(int reportId, string eventType, string payload, DateTimeOffset occurredAt)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO public.domain_events
    (workspace_id, aggregate_type, aggregate_id, event_type, event_version,
     payload, actor_user_id, actor_creator_type, occurred_at, correlation_id)
VALUES
    (@workspace_id, @aggregate_type, @aggregate_id, @event_type, @event_version,
     @payload::jsonb, @actor_user_id, @actor_creator_type, @occurred_at, @correlation_id)
RETURNING id;";

        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            workspace_id = "ws_test",
            aggregate_type = BuggetAggregateTypes.Report,
            aggregate_id = reportId.ToString(),
            event_type = eventType,
            event_version = (short)1,
            payload,
            actor_user_id = (string?)"u-test",
            actor_creator_type = (short?)null,
            occurred_at = occurredAt,
            correlation_id = (Guid?)null,
        }));
    }

    private async Task<List<IntervalRow>> ReadIntervalsAsync(int reportId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<IntervalRow>(
            @"SELECT id, report_id, phase, entered_at, exited_at, regression_cycle_index, source_event_id
              FROM public.report_phase_intervals
              WHERE report_id = @reportId
              ORDER BY entered_at, id",
            new { reportId });
        return rows.ToList();
    }

    private ReportPhaseProjectionHandler BuildHandler() =>
        new(_intervalsClient, NullLogger<ReportPhaseProjectionHandler>.Instance);

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

    private sealed class IntervalRow
    {
        public long id { get; init; }
        public int report_id { get; init; }
        public short phase { get; init; }
        public DateTimeOffset entered_at { get; init; }
        public DateTimeOffset? exited_at { get; init; }
        public int regression_cycle_index { get; init; }
        public long source_event_id { get; init; }
    }

    /// <summary>
    /// Декоратор-spy: фиксирует, какие <c>EventType</c> прошли через handler.
    /// Делегат не вызывается для событий, на которые этот handler не подписан
    /// (диспатч в poller'е по <c>EventType</c>).
    /// </summary>
    private sealed class SpyHandler(IDomainEventHandler inner) : IDomainEventHandler
    {
        public List<string> HandledEventTypes { get; } = [];

        public string EventType => inner.EventType;

        public async Task HandleAsync(DomainEventDbModel evt, IDbConnection connection, IDbTransaction transaction, CancellationToken ct)
        {
            HandledEventTypes.Add(evt.EventType);
            await inner.HandleAsync(evt, connection, transaction, ct);
        }
    }
}
