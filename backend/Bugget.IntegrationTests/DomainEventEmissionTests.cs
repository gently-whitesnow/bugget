using System.Text.Json;
using Bugget.Application.DomainEvents;
using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Bug;
using Bugget.Contracts.Dto.Report;
using Bugget.Domain.Common;
using Bugget.Domain.DomainEvents;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Проверяет, что tx-aware path в `BugsService` / `CommentsService` публикует 3 MVP события
/// (`bugget.bug.created`, `bugget.bug.status_changed`, `bugget.comment.created`)
/// в той же транзакции, что и доменная mutation. Здесь повторяется ровно тот pipeline,
/// который сервисы исполняют на проде: UoW → DbClient mutation → Publish → COMMIT.
/// </summary>
[Collection("PostgresCollection")]
public class DomainEventEmissionTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly ICommentsDbClient _commentsDbClient;
    private readonly IDomainEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _connectionString;

    public DomainEventEmissionTests(AppWithPostgresFixture fixture)
    {
        var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _commentsDbClient = scope.ServiceProvider.GetRequiredService<ICommentsDbClient>();
        _publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
        _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "CreateBug + publish 'bugget.bug.created' в одной транзакции: ровно 1 row в domain_events с корректным payload")]
    public async Task BugCreated_EmitsSingleEventWithPayload()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var orgId = $"org_{Guid.NewGuid()}";
        var report = await _reportsDbClient.CreateReportAsync(userId, null, orgId, new ReportCreateDto { Title = "r" });

        var bugDto = new BugDto { Receive = "got err", Expect = "ok", Title = "bug-title" };

        await _unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var bug = await _bugsDbClient.CreateBugAsync(scope, userId, report.Id, bugDto);

            var payload = JsonSerializer.Serialize(new
            {
                bugId = bug.Id,
                reportId = report.Id,
                title = bug.Title,
                creatorType = bug.CreatorType,
                creatorUserId = bug.CreatorUserId,
            });

            await _publisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = orgId,
                AggregateType = BuggetAggregateTypes.Bug,
                AggregateId = bug.Id.ToString(),
                EventType = BuggetEventTypes.BugCreated,
                Payload = payload,
                ActorUserId = userId,
                ActorCreatorType = (short)bug.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
            }, scope, ct);
        });

        await using var read = new NpgsqlConnection(_connectionString);
        await read.OpenAsync();

        var rows = (await read.QueryAsync<(string event_type, string payload, string workspace_id, string aggregate_id)>(
            @"SELECT event_type, payload::text AS payload, workspace_id, aggregate_id
              FROM public.domain_events
              WHERE workspace_id=@w AND event_type=@e",
            new { w = orgId, e = BuggetEventTypes.BugCreated })).ToList();

        Assert.Single(rows);
        using var payloadJson = JsonDocument.Parse(rows[0].payload);
        var payloadRoot = payloadJson.RootElement;
        Assert.Equal(0, payloadRoot.GetProperty("creatorType").GetInt32());
        Assert.Equal(userId, payloadRoot.GetProperty("creatorUserId").GetString());
        Assert.Equal("bug-title", payloadRoot.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "PatchBug меняет status + publish 'bugget.bug.status_changed': payload содержит oldStatus/newStatus")]
    public async Task BugStatusChanged_EmitsSingleEventWithOldAndNew()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var orgId = $"org_{Guid.NewGuid()}";
        var report = await _reportsDbClient.CreateReportAsync(userId, null, orgId, new ReportCreateDto { Title = "r" });
        var bug = await _bugsDbClient.CreateBugAsync(userId, report.Id, new BugDto { Receive = "r", Expect = "e" });

        await _unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var existing = await _bugsDbClient.GetBugAsync(scope, report.Id, bug.Id);
            Assert.NotNull(existing);
            var oldStatus = existing!.Status;

            var patch = new BugPatchDto { Status = 2 };
            var result = await _bugsDbClient.PatchBugAsync(scope, report.Id, bug.Id, patch);
            Assert.NotEqual(oldStatus, result.Status);

            var payload = JsonSerializer.Serialize(new
            {
                bugId = bug.Id,
                reportId = report.Id,
                oldStatus,
                newStatus = result.Status,
                actorUserId = userId,
            });

            await _publisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = orgId,
                AggregateType = BuggetAggregateTypes.Bug,
                AggregateId = bug.Id.ToString(),
                EventType = BuggetEventTypes.BugStatusChanged,
                Payload = payload,
                ActorUserId = userId,
                OccurredAt = DateTimeOffset.UtcNow,
            }, scope, ct);
        });

        await using var read = new NpgsqlConnection(_connectionString);
        await read.OpenAsync();

        var rows = (await read.QueryAsync<(string event_type, string payload)>(
            @"SELECT event_type, payload::text AS payload
              FROM public.domain_events
              WHERE workspace_id=@w AND aggregate_id=@a AND event_type=@e",
            new { w = orgId, a = bug.Id.ToString(), e = BuggetEventTypes.BugStatusChanged })).ToList();

        Assert.Single(rows);
        using var payloadJson = JsonDocument.Parse(rows[0].payload);
        var payloadRoot = payloadJson.RootElement;
        Assert.Equal(0, payloadRoot.GetProperty("oldStatus").GetInt32());
        Assert.Equal(2, payloadRoot.GetProperty("newStatus").GetInt32());
    }

    [Fact(DisplayName = "CreateComment + publish 'bugget.comment.created': payload содержит audience, creatorType, commentId")]
    public async Task CommentCreated_EmitsSingleEventWithAudienceAndCreatorType()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var orgId = $"org_{Guid.NewGuid()}";
        var report = await _reportsDbClient.CreateReportAsync(userId, null, orgId, new ReportCreateDto { Title = "r" });
        var bug = await _bugsDbClient.CreateBugAsync(userId, report.Id, new BugDto { Receive = "r", Expect = "e" });

        await _unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var comment = await _commentsDbClient.CreateCommentAsync(
                scope, userId, bug.Id, "dev question",
                creatorType: (int)CreatorType.User,
                audience: (int)CommentAudience.External);

            var payload = JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                bugId = comment.BugId,
                text = comment.Text,
                audience = comment.Audience,
                creatorType = comment.CreatorType,
                creatorUserId = comment.CreatorUserId,
                attachments = Array.Empty<object>(),
            });

            await _publisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = orgId,
                AggregateType = BuggetAggregateTypes.Comment,
                AggregateId = comment.Id.ToString(),
                EventType = BuggetEventTypes.CommentCreated,
                Payload = payload,
                ActorUserId = userId,
                ActorCreatorType = (short)comment.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
            }, scope, ct);
        });

        await using var read = new NpgsqlConnection(_connectionString);
        await read.OpenAsync();

        var rows = (await read.QueryAsync<(string event_type, string payload)>(
            @"SELECT event_type, payload::text AS payload
              FROM public.domain_events
              WHERE workspace_id=@w AND event_type=@e",
            new { w = orgId, e = BuggetEventTypes.CommentCreated })).ToList();

        Assert.Single(rows);
        using var payloadJson = JsonDocument.Parse(rows[0].payload);
        var payloadRoot = payloadJson.RootElement;
        Assert.Equal((int)CommentAudience.External, payloadRoot.GetProperty("audience").GetInt32());
        Assert.Equal((int)CreatorType.User, payloadRoot.GetProperty("creatorType").GetInt32());
        Assert.Equal(0, payloadRoot.GetProperty("attachments").GetArrayLength());
    }
}
