using System.Collections.Concurrent;
using Bugget.DA.WebSockets;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DbModels.Comment;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.SocketViews;

namespace Bugget.IntegrationTests.Fixtures;

/// <summary>
/// Тестовый фейк, чтобы интеграционные тесты могли утверждать, что в SignalR-хаб
/// был отправлен конкретный event без поднятия реального хаба.
/// </summary>
public sealed class FakeReportPageHubClient : IReportPageHubClient
{
    public ConcurrentBag<(string GroupKey, CommentSummaryDbModel Comment)> CommentCreates { get; } = new();
    public ConcurrentBag<(string GroupKey, AttachmentSocketView Attachment)> AttachmentCreates { get; } = new();

    public Task SendCommentCreateAsync(string groupKey, CommentSummaryDbModel commentSummaryDbModel, string? signalRConnectionId)
    {
        CommentCreates.Add((groupKey, commentSummaryDbModel));
        return Task.CompletedTask;
    }

    public Task SendReportPatchAsync(string groupKey, PatchReportSocketView view, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendNewReportParticipantAsync(string groupKey, string newParticipant) => Task.CompletedTask;
    public Task SendBugCreateAsync(string groupKey, BugSummaryDbModel summaryDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugPatchAsync(string groupKey, int bugId, BugPatchDto patchDto, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendAttachmentCreateAsync(string groupKey, AttachmentSocketView attachmentSocketView, string? signalRConnectionId)
    {
        AttachmentCreates.Add((groupKey, attachmentSocketView));
        return Task.CompletedTask;
    }
    public Task SendAttachmentDeleteAsync(string groupKey, int id, int entityId, int attachType, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendAttachmentChangedAsync(string groupKey, AttachmentSocketView attachmentSocketView) => Task.CompletedTask;
    public Task SendCommentDeleteAsync(string groupKey, int bugId, int commentId, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendCommentUpdateAsync(string groupKey, CommentSummaryDbModel commentSummaryDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkCreateAsync(string groupKey, ReportLinkDbModel linkDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkUpdateAsync(string groupKey, ReportLinkDbModel linkDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendReportLinkDeleteAsync(string groupKey, int linkId, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepCreateAsync(string groupKey, BugStepSummaryDbModel bugStepSummaryDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepPatchAsync(string groupKey, int bugId, BugStepSummaryDbModel bugStepSummaryDbModel, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepsOrderUpdateAsync(string groupKey, int bugId, BugStepSummaryDbModel[] bugStepSummaryDbModels, string? signalRConnectionId) => Task.CompletedTask;
    public Task SendBugStepDeleteAsync(string groupKey, int bugId, int stepId, string? signalRConnectionId) => Task.CompletedTask;
}
