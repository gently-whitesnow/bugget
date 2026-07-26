using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.Tests.DomainEvents;

/// <summary>
/// Контракт фабрики `ReportStatusEventFactory`: дедупликация `from == to`,
/// snake_case payload, корректные строковые имена enum-значений `ReportStatus`,
/// корректный `event_type` / `aggregate_type`.
/// </summary>
public class ReportStatusEventFactoryTests
{
    [Fact(DisplayName = "from == to: фабрика возвращает null (инвариант дедупликации)")]
    public void TryCreate_ReturnsNull_WhenFromEqualsTo()
    {
        var evt = ReportStatusEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 42,
            fromStatus: ReportStatus.Fix,
            toStatus: ReportStatus.Fix,
            actorUserId: "u1");

        Assert.Null(evt);
    }

    [Fact(DisplayName = "from != to: фабрика возвращает event с корректными мета-полями")]
    public void TryCreate_ReturnsEvent_WithCorrectMetadata()
    {
        var evt = ReportStatusEventFactory.TryCreate(
            workspaceId: "ws-123",
            reportId: 42,
            fromStatus: ReportStatus.Test,
            toStatus: ReportStatus.Fix,
            actorUserId: "u1",
            actorCreatorType: 1);

        Assert.NotNull(evt);
        Assert.Equal("ws-123", evt!.WorkspaceId);
        Assert.Equal(BuggetAggregateTypes.Report, evt.AggregateType);
        Assert.Equal("42", evt.AggregateId);
        Assert.Equal(BuggetEventTypes.ReportStatusChanged, evt.EventType);
        Assert.Equal("u1", evt.ActorUserId);
        Assert.Equal((short)1, evt.ActorCreatorType);
        Assert.NotNull(evt.CorrelationId);
    }

    [Fact(DisplayName = "Payload — snake_case с именами enum-значений ReportStatus в качестве строк")]
    public void TryCreate_PayloadIsSnakeCaseWithEnumNames()
    {
        var evt = ReportStatusEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 42,
            fromStatus: ReportStatus.Test,
            toStatus: ReportStatus.Fix,
            actorUserId: "u1");

        Assert.NotNull(evt);
        using var doc = JsonDocument.Parse(evt!.Payload);
        Assert.Equal("Test", doc.RootElement.GetProperty("from_status").GetString());
        Assert.Equal("Fix", doc.RootElement.GetProperty("to_status").GetString());

        // sanity: camelCase ключей нет — только snake_case
        Assert.False(doc.RootElement.TryGetProperty("fromStatus", out _));
        Assert.False(doc.RootElement.TryGetProperty("toStatus", out _));
    }

    [Theory(DisplayName = "Имена всех значений ReportStatus сохраняются дословно в payload")]
    [InlineData(ReportStatus.Backlog, "Backlog")]
    [InlineData(ReportStatus.Resolved, "Resolved")]
    [InlineData(ReportStatus.Fix, "Fix")]
    [InlineData(ReportStatus.Rejected, "Rejected")]
    [InlineData(ReportStatus.Test, "Test")]
    public void TryCreate_SerializesEnumValueName(ReportStatus status, string expected)
    {
        var evt = ReportStatusEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 1,
            fromStatus: status,
            toStatus: status == ReportStatus.Backlog ? ReportStatus.Fix : ReportStatus.Backlog,
            actorUserId: "u1");

        Assert.NotNull(evt);
        using var doc = JsonDocument.Parse(evt!.Payload);
        Assert.Equal(expected, doc.RootElement.GetProperty("from_status").GetString());
    }

    [Fact(DisplayName = "actorUserId=null допустим (auto-переход без user actor)")]
    public void TryCreate_AcceptsNullActor()
    {
        var evt = ReportStatusEventFactory.TryCreate(
            workspaceId: "ws",
            reportId: 1,
            fromStatus: ReportStatus.Backlog,
            toStatus: ReportStatus.Fix,
            actorUserId: null);

        Assert.NotNull(evt);
        Assert.Null(evt!.ActorUserId);
    }
}
