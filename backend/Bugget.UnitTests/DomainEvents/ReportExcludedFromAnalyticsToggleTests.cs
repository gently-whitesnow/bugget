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
using Xunit;

namespace Bugget.UnitTests.DomainEvents;

/// <summary>
/// T11 · TECHSPEC §4.5. PATCH `/v2/reports/{id}` с полем
/// `is_excluded_from_analytics`. Контракт:
/// <list type="bullet">
///   <item>Изменение значения → domain event
///         <c>bugget.report.excluded_from_analytics_toggled</c>
///         с payload <c>{ is_excluded: bool }</c>.</item>
///   <item>Patch с тем же значением → ни события, ни попытки эмиссии нет.</item>
///   <item>Эмиссия идёт в той же транзакции, что и UPDATE (publisher
///         вызывается со scope, который пробросил UnitOfWork).</item>
/// </list>
/// </summary>
public class ReportExcludedFromAnalyticsToggleTests
{
    private static IUnitOfWork CreatePassThroughUnitOfWork()
    {
        var scope = new Mock<ITransactionScope>().Object;
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>, CancellationToken>(
                (action, ct) => action(scope, ct));
        return uow.Object;
    }

    private static UserIdentity CreateUser(string id, string organizationId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, id) }, "test");
        identity.AddClaim(new Claim(AuthClaims.OrganizationId, organizationId));
        return new UserIdentity(new ClaimsPrincipal(identity));
    }

    private static ReportPatchResult BuildPatchResult(int reportId, bool isExcluded) => new()
    {
        Id = reportId,
        PublicId = Guid.NewGuid(),
        Title = "t",
        Status = (int)ReportStatus.Backlog,
        ResponsibleUserId = "responsible",
        PastResponsibleUserId = "past",
        UpdatedAt = DateTimeOffset.UtcNow,
        CreatorTeamId = null,
        IsExcludedFromAnalytics = isExcluded,
    };

    private static ITaskQueue NoOpTaskQueue()
    {
        var q = new Mock<ITaskQueue>();
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<Task>>())).Returns(ValueTask.CompletedTask);
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<CancellationToken, Task>>())).Returns(ValueTask.CompletedTask);
        q.Setup(x => x.EnqueueAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>())).Returns(ValueTask.CompletedTask);
        return q.Object;
    }

    private static (Mock<IReportsDbClient> Db, Mock<IDomainEventPublisher> Publisher, ReportsService Svc, List<DomainEvent> Events) BuildSut(
        int reportId,
        bool? currentIsExcluded)
    {
        var db = new Mock<IReportsDbClient>();
        db.Setup(x => x.ResolveReportIdAsync(It.IsAny<string>(), It.IsAny<string>(), reportId, It.IsAny<Guid?>(), It.IsAny<int?>()))
            .ReturnsAsync(new ResolvedReportId { Id = reportId, CreatorTeamId = null });
        db.Setup(x => x.GetIsExcludedFromAnalyticsAsync(It.IsAny<ITransactionScope>(), reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIsExcluded);
        db.Setup(x => x.PatchReportAsync(reportId, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, ReportPatchDto dto, ITransactionScope? _, CancellationToken _) =>
                BuildPatchResult(reportId, dto.IsExcludedFromAnalytics ?? currentIsExcluded ?? false));

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

        return (db, publisher, svc, events);
    }

    [Fact(DisplayName = "Toggle false→true: эмитим событие, payload = { is_excluded: true }")]
    public async Task Toggle_FalseToTrue_EmitsEvent()
    {
        var (db, _, svc, events) = BuildSut(reportId: 42, currentIsExcluded: false);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { IsExcludedFromAnalytics = true });

        var evt = Assert.Single(events);
        Assert.Equal(BuggetEventTypes.ReportExcludedFromAnalyticsToggled, evt.EventType);
        Assert.Equal(BuggetAggregateTypes.Report, evt.AggregateType);
        Assert.Equal("42", evt.AggregateId);
        Assert.Equal("org-1", evt.WorkspaceId);
        Assert.Equal("u1", evt.ActorUserId);

        using var doc = JsonDocument.Parse(evt.Payload);
        Assert.True(doc.RootElement.GetProperty("is_excluded").GetBoolean());

        // UPDATE прошёл с тем же значением, что в DTO.
        db.Verify(x => x.PatchReportAsync(
            42,
            It.Is<ReportPatchDto>(d => d.IsExcludedFromAnalytics == true),
            It.IsAny<ITransactionScope?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Toggle true→false: эмитим событие, payload = { is_excluded: false }")]
    public async Task Toggle_TrueToFalse_EmitsEvent()
    {
        var (_, _, svc, events) = BuildSut(reportId: 42, currentIsExcluded: true);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { IsExcludedFromAnalytics = false });

        var evt = Assert.Single(events);
        Assert.Equal(BuggetEventTypes.ReportExcludedFromAnalyticsToggled, evt.EventType);
        using var doc = JsonDocument.Parse(evt.Payload);
        Assert.False(doc.RootElement.GetProperty("is_excluded").GetBoolean());
    }

    [Fact(DisplayName = "Toggle с тем же значением: нет события, UPDATE всё равно выполняется (no-op значение)")]
    public async Task Toggle_NoChange_NoEvent()
    {
        var (db, publisher, svc, _) = BuildSut(reportId: 42, currentIsExcluded: true);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { IsExcludedFromAnalytics = true });

        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
        // UPDATE всё ещё происходит — это контракт patch_report_internal (COALESCE);
        // отвечает за «не писать в outbox при равенстве» сервис, не DB-функция.
        db.Verify(x => x.PatchReportAsync(42, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "PATCH без IsExcludedFromAnalytics: pre-fetch флага не делается, нет события")]
    public async Task Patch_WithoutFlag_NoPreFetchNoEvent()
    {
        var (db, publisher, svc, _) = BuildSut(reportId: 42, currentIsExcluded: false);

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("42", user, new ReportPatchDto { Title = "rename" });

        publisher.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(x => x.GetIsExcludedFromAnalyticsAsync(It.IsAny<ITransactionScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Toggle одновременно со сменой Status: эмитятся оба события (status_changed + toggled)")]
    public async Task Toggle_WithStatusChange_BothEventsEmitted()
    {
        // current status = Backlog, currentIsExcluded = false.
        var db = new Mock<IReportsDbClient>();
        db.Setup(x => x.ResolveReportIdAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<Guid?>(), It.IsAny<int?>()))
            .ReturnsAsync(new ResolvedReportId { Id = 7, CreatorTeamId = null });
        db.Setup(x => x.GetStatusAndResponsibleAsync(It.IsAny<ITransactionScope>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((int)ReportStatus.Backlog, (string?)null));
        db.Setup(x => x.GetIsExcludedFromAnalyticsAsync(It.IsAny<ITransactionScope>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        db.Setup(x => x.PatchReportAsync(7, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, ReportPatchDto dto, ITransactionScope? _, CancellationToken _) =>
                new ReportPatchResult
                {
                    Id = 7,
                    PublicId = Guid.NewGuid(),
                    Title = "t",
                    Status = dto.Status ?? (int)ReportStatus.Backlog,
                    ResponsibleUserId = "responsible",
                    PastResponsibleUserId = "past",
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatorTeamId = null,
                    IsExcludedFromAnalytics = dto.IsExcludedFromAnalytics ?? false,
                });

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

        var user = CreateUser("u1", "org-1");
        await svc.PatchReportAsync("7", user, new ReportPatchDto
        {
            Status = (int)ReportStatus.Fix,
            IsExcludedFromAnalytics = true,
        });

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.EventType == BuggetEventTypes.ReportStatusChanged);
        Assert.Contains(events, e => e.EventType == BuggetEventTypes.ReportExcludedFromAnalyticsToggled);
    }
}
