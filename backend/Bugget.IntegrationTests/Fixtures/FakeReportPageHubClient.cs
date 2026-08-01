using System.Collections.Concurrent;
using Bugget.Application.Commands.Bug;
using Bugget.Application.Ports;
using Bugget.Application.Realtime;
using Bugget.Domain.Bugs;
using Bugget.Domain.Comments;
using Bugget.Domain.Reports;

namespace Bugget.IntegrationTests.Fixtures;

/// <summary>
/// Тестовый фейк, чтобы интеграционные тесты могли утверждать, что в SignalR-хаб
/// был отправлен конкретный event без поднятия реального хаба.
/// </summary>
public sealed class FakeReportPageHubClient : IReportPageHubClient
{
    public ConcurrentBag<(string GroupKey, CommentSummary Comment)> CommentCreates { get; } = new();
    public ConcurrentBag<(string GroupKey, AttachmentSocketView Attachment)> AttachmentCreates { get; } = new();

    public Task SendCommentCreateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId)
    {
        CommentCreates.Add((groupKey, comment));
        return Task.CompletedTask;
    }

    public Task SendReportPatchAsync(string groupKey, PatchReportSocketView view, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendNewReportParticipantAsync(string groupKey, string newParticipant) => Task.CompletedTask;
    public Task SendBugCreateAsync(string groupKey, BugSummary summary, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugPatchAsync(string groupKey, int bugId, BugPatchDto patchDto, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendAttachmentCreateAsync(string groupKey, AttachmentSocketView attachmentSocketView, string? signalRConnectionId)
    {
        AttachmentCreates.Add((groupKey, attachmentSocketView));
        return Task.CompletedTask;
    }
    public Task SendAttachmentDeleteAsync(string groupKey, int id, int entityId, int attachType, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendAttachmentChangedAsync(string groupKey, AttachmentSocketView attachmentSocketView) => Task.CompletedTask;
    public Task SendCommentDeleteAsync(string groupKey, int bugId, int commentId, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendCommentUpdateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkCreateAsync(string groupKey, ReportLink link, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkUpdateAsync(string groupKey, ReportLink link, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkDeleteAsync(string groupKey, int linkId, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepCreateAsync(string groupKey, BugStepSummary step, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepPatchAsync(string groupKey, int bugId, BugStepSummary step, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepsOrderUpdateAsync(string groupKey, int bugId, BugStepSummary[] steps, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepDeleteAsync(string groupKey, int bugId, int stepId, string? signalRConnectionId) => Task.CompletedTask;
}
