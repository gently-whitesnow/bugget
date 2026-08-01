using System.Text.Json;
using Bugget.Application.DomainEvents;

namespace Bugget.UnitTests.DomainEvents;

/// <summary>
/// Контракт фабрики <see cref="ReportExcludedEventFactory"/>: дедупликация
/// <c>old == new</c>, snake_case payload, корректный <c>event_type</c> / <c>aggregate_type</c>.
/// </summary>
public class ReportExcludedEventFactoryTests
{
    [Fact(DisplayName = "old == new: фабрика возвращает null (инвариант дедупликации)")]
    public void TryCreate_ReturnsNull_WhenOldEqualsNew()
    {
        Assert.Null(ReportExcludedEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 42,
            oldIsExcluded: true,
            newIsExcluded: true,
            actorUserId: "u1"));

        Assert.Null(ReportExcludedEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 42,
            oldIsExcluded: false,
            newIsExcluded: false,
            actorUserId: "u1"));
    }

    [Fact(DisplayName = "false→true: событие с корректными мета-полями и payload { is_excluded: true }")]
    public void TryCreate_FalseToTrue_ReturnsEvent()
    {
        var evt = ReportExcludedEventFactory.TryCreate(
            workspaceId: "ws-123",
            reportId: 42,
            oldIsExcluded: false,
            newIsExcluded: true,
            actorUserId: "u1",
            actorCreatorType: 1);

        Assert.NotNull(evt);
        Assert.Equal("ws-123", evt!.WorkspaceId);
        Assert.Equal(BuggetAggregateTypes.Report, evt.AggregateType);
        Assert.Equal("42", evt.AggregateId);
        Assert.Equal(BuggetEventTypes.ReportExcludedFromAnalyticsToggled, evt.EventType);
        Assert.Equal("u1", evt.ActorUserId);
        Assert.Equal((short)1, evt.ActorCreatorType);
        Assert.NotNull(evt.CorrelationId);

        using var doc = JsonDocument.Parse(evt.Payload);
        Assert.True(doc.RootElement.GetProperty("is_excluded").GetBoolean());

        // sanity: camelCase ключей нет
        Assert.False(doc.RootElement.TryGetProperty("isExcluded", out _));
    }

    [Fact(DisplayName = "true→false: payload { is_excluded: false }")]
    public void TryCreate_TrueToFalse_ReturnsEvent()
    {
        var evt = ReportExcludedEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 42,
            oldIsExcluded: true,
            newIsExcluded: false,
            actorUserId: "u1");

        Assert.NotNull(evt);
        using var doc = JsonDocument.Parse(evt!.Payload);
        Assert.False(doc.RootElement.GetProperty("is_excluded").GetBoolean());
    }

    [Fact(DisplayName = "actorUserId=null допустим (системный actor)")]
    public void TryCreate_AcceptsNullActor()
    {
        var evt = ReportExcludedEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 1,
            oldIsExcluded: false,
            newIsExcluded: true,
            actorUserId: null);

        Assert.NotNull(evt);
        Assert.Null(evt!.ActorUserId);
    }
}
