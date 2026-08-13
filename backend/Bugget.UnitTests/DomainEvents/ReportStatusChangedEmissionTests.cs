using System.Security.Claims;
using System.Text.Json;
using Bugget.Application.Commands.Report;
using Bugget.Application.DomainEvents;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.DomainEvents;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;
using Moq;

namespace Bugget.UnitTests.DomainEvents;

/// <summary>
/// Эмиссия `bugget.report.status_changed` через `ReportsService.PatchReportAsync`:
/// manual override, auto-status driver по фактической смене responsible_user_id
/// (Backlog→Fix, Fix→Test, Test→Fix; Resolved/Rejected — терминалы),
/// дедупликация `from == to` фабрикой.
/// </summary>
public class ReportStatusChangedEmissionTests
{
    private static IUnitOfWork CreatePassThroughUnitOfWork()
    {
        var scope = new Mock<ITransactionScope>().Object;
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>, CancellationToken>(
                (action, ct) => action(scope, ct));
        uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task<ReportPatchResult>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task<ReportPatchResult>>, CancellationToken>(
                (action, ct) => action(scope, ct));
        uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(scope, ct));
        return uow.Object;
    }

    private static UserIdentity CreateUser(string id, string organizationId, string? teamId = null)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, id) }, "test");
        var claims = new List<Claim> { new(AuthClaims.OrganizationId, organizationId) };
        if (teamId != null)
        {
            claims.Add(new Claim(AuthClaims.TeamId, teamId));
        }
        identity.AddClaims(claims);
        return new UserIdentity(new ClaimsPrincipal(identity));
    }

    private static ReportPatchResult BuildPatchResult(int reportId, int status, string responsibleUserId = "responsible", string pastResponsibleUserId = "past") => new()
    {
        Id = reportId,
        PublicId = Guid.NewGuid(),
        Title = "t",
        Status = status,
        ResponsibleUserId = responsibleUserId,
        PastResponsibleUserId = pastResponsibleUserId,
        UpdatedAt = DateTimeOffset.UtcNow,
        CreatorTeamId = null,
    };

    private static ITaskQueue NoOpTaskQueue()
    {
        var q = new Mock<ITaskQueue>();
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<Task>>())).Returns(ValueTask.CompletedTask);
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<CancellationToken, Task>>())).Returns(ValueTask.CompletedTask);
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>())).Returns(ValueTask.CompletedTask);
        return q.Object;
    }

    /// <summary>
    /// Создаёт стандартный мок IReportsDbClient: пробрасывает Resolved + pre-fetch
    /// (status, responsible) + PATCH, который запоминает фактически переданный DTO,
    /// чтобы тесты могли утверждать, какой Status в итоге уйдёт в БД.
    /// </summary>
    private static (Mock<IReportsDbClient> Db, List<ReportPatchDto> ObservedDtos) BuildReportsDbMock(
        int reportId,
        int currentStatus,
        string? currentResponsibleUserId,
        Func<ReportPatchDto, ReportPatchResult>? buildResult = null)
    {
        var db = new Mock<IReportsDbClient>();
        db.Setup(x => x.ResolveReportIdAsync(It.IsAny<string>(), It.IsAny<string>(), reportId, It.IsAny<Guid?>(), It.IsAny<int?>()))
            .ReturnsAsync(new ResolvedReportId { Id = reportId, CreatorTeamId = null });
        db.Setup(x => x.GetPatchSnapshotAsync(It.IsAny<ITransactionScope>(), reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportPatchSnapshot(currentStatus, currentResponsibleUserId, PastResponsibleUserId: null, CreatorUserId: "creator"));

        var observed = new List<ReportPatchDto>();
        db.Setup(x => x.PatchReportAsync(reportId, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()))
            .Callback<int, ReportPatchDto, ITransactionScope?, CancellationToken>((_, dto, _, _) => observed.Add(dto))
            .ReturnsAsync((int _, ReportPatchDto dto, ITransactionScope? _, CancellationToken _) =>
                buildResult != null
                    ? buildResult(dto)
                    // По умолчанию: возвращаемый Status = тому, что попал в DTO (или прежнему, если не задан).
                    : BuildPatchResult(reportId, dto.Status ?? currentStatus, dto.ResponsibleUserId ?? currentResponsibleUserId ?? "responsible"));
        return (db, observed);
    }

    private static (ReportsService Svc, Mock<IDomainEventPublisher> Publisher, List<DomainEvent> Events) BuildSut(
        Mock<IReportsDbClient> db)
    {
        var events = new List<DomainEvent>();
        var publisher = new Mock<IDomainEventPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()))
            .Callback<DomainEvent, ITransactionScope, CancellationToken>((e, _, _) => events.Add(e))
            .ReturnsAsync(1L);

        var svc = new ReportsService(
            db.Object,
            NoOpTaskQueue(),
            reportEventsService: null!,
            publisher.Object,
            CreatePassThroughUnitOfWork(),
            Options.Create(new ReportAliasOptions { AliasMode = ReportAliasMode.Default }));
        return (svc, publisher, events);
    }

    // ============ Manual override ============

    [Fact(DisplayName = "Manual: явный Status=Fix из Backlog → событие Backlog→Fix")]
    public async Task ManualOverride_StatusChanged_EmitsEvent()
    {
        var (db, _) = BuildReportsDbMock(42, (int)ReportStatus.Backlog, currentResponsibleUserId: null);
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        var result = await svc.PatchReportAsync("42", user, new ReportPatchDto { Status = (int)ReportStatus.Fix });

        Assert.True(result.Error is null);
        Assert.Single(events);
        var evt = events[0];
        Assert.Equal(BuggetEventTypes.ReportStatusChanged, evt.EventType);
        Assert.Equal("42", evt.AggregateId);
        Assert.Equal("org-1", evt.WorkspaceId);
        Assert.Equal("u1", evt.ActorUserId);

        using var doc = JsonDocument.Parse(evt.Payload);
        Assert.Equal("Backlog", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Fix", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "PATCH без Status и без ResponsibleUserId: pre-fetch не делается, эмиссии нет")]
    public async Task PatchWithoutStatusOrResponsible_DoesNotEmit()
    {
        var db = new Mock<IReportsDbClient>();
        db.Setup(x => x.ResolveReportIdAsync(It.IsAny<string>(), It.IsAny<string>(), 42, It.IsAny<Guid?>(), It.IsAny<int?>()))
            .ReturnsAsync(new ResolvedReportId { Id = 42, CreatorTeamId = null });
        db.Setup(x => x.PatchReportAsync(42, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPatchResult(42, (int)ReportStatus.Backlog));

        var (svc, publisher, _) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { Title = "renamed" });

        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(x => x.GetPatchSnapshotAsync(It.IsAny<ITransactionScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Manual: явный Status, но from == to — дедупликация, эмиссии нет")]
    public async Task ManualOverride_StatusUnchanged_Deduplicated()
    {
        var (db, _) = BuildReportsDbMock(42, (int)ReportStatus.Fix, currentResponsibleUserId: "u-prev");
        var (svc, publisher, _) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { Status = (int)ReportStatus.Fix });

        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============ Auto-status driver ============

    [Fact(DisplayName = "Auto: Backlog (resp=null) + смена responsible → Fix, событие Backlog→Fix")]
    public async Task AutoDriver_BacklogWithNullResponsible_To_Fix()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Backlog, currentResponsibleUserId: null);
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        var result = await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-new" });

        Assert.True(result.Error is null);
        // DTO, ушедший в БД, содержит auto-выставленный Status=Fix
        // (первый ответственный начинает фиксить, Test — вторая фаза).
        Assert.Equal((int)ReportStatus.Fix, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Backlog", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Fix", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Auto: Test + смена responsible → Fix, событие Test→Fix")]
    public async Task AutoDriver_Test_To_Fix()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Test, currentResponsibleUserId: "u-a");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-b" });

        Assert.Equal((int)ReportStatus.Fix, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Test", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Fix", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Auto: Fix + смена responsible → Test, событие Fix→Test")]
    public async Task AutoDriver_Fix_To_Test()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Fix, currentResponsibleUserId: "u-a");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-b" });

        Assert.Equal((int)ReportStatus.Test, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Fix", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Test", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Manual override > driver: смена responsible + явный Status=Resolved → status=Resolved, событие Test→Resolved")]
    public async Task ManualOverride_BeatsAutoDriver()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Test, currentResponsibleUserId: "u-a");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto
        {
            ResponsibleUserId = "u-b",
            Status = (int)ReportStatus.Resolved,
        });

        // Driver НЕ применился — Status в DTO остался Resolved, не Fix.
        Assert.Equal((int)ReportStatus.Resolved, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Test", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Resolved", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Theory(DisplayName = "Терминалы: смена responsible в Resolved/Rejected — status не меняется, эмиссии нет")]
    [InlineData((int)ReportStatus.Resolved)]
    [InlineData((int)ReportStatus.Rejected)]
    public async Task AutoDriver_TerminalStatuses_NotRecomputed(int terminal)
    {
        var (db, observed) = BuildReportsDbMock(7, terminal, currentResponsibleUserId: "u-a");
        var (svc, publisher, _) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-b" });

        // Status в DTO так и остался null (driver не сработал).
        Assert.Null(observed.Single().Status);
        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Manual override → Backlog из Fix — допустимо: status=Backlog, событие Fix→Backlog")]
    public async Task ManualOverride_BackToBacklog_FromFix()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Fix, currentResponsibleUserId: "u-a");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { Status = (int)ReportStatus.Backlog });

        Assert.Equal((int)ReportStatus.Backlog, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Fix", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Backlog", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Manual override → Backlog из Test — допустимо: status=Backlog, событие Test→Backlog")]
    public async Task ManualOverride_BackToBacklog_FromTest()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Test, currentResponsibleUserId: "u-a");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { Status = (int)ReportStatus.Backlog });

        Assert.Equal((int)ReportStatus.Backlog, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Test", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Backlog", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Auto: Backlog с уже выставленным responsible + смена responsible → Fix")]
    public async Task AutoDriver_BacklogWithExistingResponsible_To_Fix()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Backlog, currentResponsibleUserId: "u-prev");
        var (svc, _, events) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-next" });

        Assert.Equal((int)ReportStatus.Fix, observed.Single().Status);

        Assert.Single(events);
        using var doc = JsonDocument.Parse(events[0].Payload);
        Assert.Equal("Backlog", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Fix", doc.RootElement.GetProperty("to_status").GetString());
    }

    [Fact(DisplayName = "Auto: PATCH с тем же responsible не пересчитывает статус")]
    public async Task AutoDriver_SameResponsible_NotRecomputed()
    {
        var (db, observed) = BuildReportsDbMock(7, (int)ReportStatus.Test, currentResponsibleUserId: "u-a");
        var (svc, publisher, _) = BuildSut(db);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto { ResponsibleUserId = "u-a" });

        Assert.Null(observed.Single().Status);
        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
