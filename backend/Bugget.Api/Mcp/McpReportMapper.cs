using Bugget.Application.Results.Reports;
using Bugget.Domain.Attachments;
using Bugget.Domain.Bugs;
using Bugget.Domain.Comments;
using Bugget.Domain.Reports;

namespace Bugget.Api.Mcp;

/// <summary>
/// Доменная модель репорта → проекции MCP. Вход — <see cref="ReportViewModel"/>, а не <see cref="Report"/>:
/// alias-ид считает application-слой, пересчёт здесь завёл бы второй источник правды.
/// </summary>
internal static class McpReportMapper
{
    public static McpReportPage ToPage(long total, int skip, int take, ReportViewModel[] reports) =>
        new(total, skip, take, [.. reports.Select(ToListItem)]);

    public static McpReport ToReport(ReportViewModel report) =>
        new(
            report.Id,
            report.Title,
            McpWire.FormatReportStatus(report.Status),
            report.CreatorUserId,
            report.ResponsibleUserId,
            report.CreatorTeamId,
            McpWire.FormatCreatorType(report.CreatorType),
            report.CreatedAt,
            report.UpdatedAt,
            NullIfEmpty(report.ParticipantsUserIds),
            Map(report.Links, ToLink),
            Map(report.Bugs, ToBug));

    /// <summary>Ответ create_report: без дерева и без <c>bugs_count</c> — у нового репорта он всегда ноль.</summary>
    public static McpReportSummary ToSummary(ReportSummaryViewModel report) =>
        new(
            report.Id,
            report.Title,
            McpWire.FormatReportStatus(report.Status),
            report.CreatorUserId,
            report.ResponsibleUserId,
            report.CreatorTeamId,
            McpWire.FormatCreatorType(report.CreatorType),
            report.CreatedAt,
            report.UpdatedAt);

    /// <summary>Ответ create_bug: сам баг без вложенного дерева.</summary>
    public static McpBugSummary ToBugSummary(BugSummary bug) =>
        new(
            bug.Id,
            bug.Title,
            McpWire.FormatBugStatus(bug.Status),
            bug.CreatorUserId,
            McpWire.FormatCreatorType(bug.CreatorType),
            bug.CreatedAt,
            bug.UpdatedAt,
            bug.Receive,
            bug.Expect);

    public static McpAttachmentDetails ToAttachmentDetails(
        Attachment attachment,
        string reportId,
        string downloadPath) =>
        new(
            attachment.Id,
            reportId,
            attachment.EntityId,
            McpWire.FormatAttachType(attachment.AttachType),
            attachment.FileName,
            attachment.MimeType,
            attachment.LengthBytes,
            attachment.HasPreview ?? false,
            downloadPath,
            attachment.CreatedAt,
            attachment.CreatorUserId);

    /// <summary>Вложение ищется в уже загруженном дереве: оно отфильтровано по workspace и команде.</summary>
    public static LocatedAttachment? FindAttachment(Report report, int attachmentId) =>
        (report.Bugs ?? []).SelectMany(BugAttachments).FirstOrDefault(a => a.Attachment.Id == attachmentId);

    private static IEnumerable<LocatedAttachment> BugAttachments(Bug bug) =>
        (bug.Attachments ?? []).Select(a => new LocatedAttachment(a, bug.Id, ParentId: 0))
            .Concat((bug.Comments ?? []).SelectMany(comment =>
                (comment.Attachments ?? []).Select(a => new LocatedAttachment(a, bug.Id, comment.Id))))
            .Concat((bug.Steps ?? []).SelectMany(step =>
                (step.Attachments ?? []).Select(a => new LocatedAttachment(a, bug.Id, step.Id))));

    private static McpReportListItem ToListItem(ReportViewModel report) =>
        new(
            report.Id,
            report.Title,
            McpWire.FormatReportStatus(report.Status),
            report.CreatorUserId,
            report.ResponsibleUserId,
            report.CreatorTeamId,
            McpWire.FormatCreatorType(report.CreatorType),
            report.CreatedAt,
            report.UpdatedAt,
            report.Bugs?.Length ?? 0);

    private static McpReportLink ToLink(ReportLink link) => new(link.Name, link.Link);

    private static McpBug ToBug(Bug bug) =>
        new(
            bug.Id,
            bug.Title,
            McpWire.FormatBugStatus(bug.Status),
            bug.CreatorUserId,
            McpWire.FormatCreatorType(bug.CreatorType),
            bug.CreatedAt,
            bug.UpdatedAt,
            bug.Receive,
            bug.Expect,
            Map(bug.Steps, ToStep),
            Map(bug.Comments, ToComment),
            Map(bug.Attachments, ToAttachment));

    /// <summary>Шаг в дереве и ответ create/update_bug_step — одна форма, чтобы модель не видела два вида шага.</summary>
    public static McpBugStep ToStep(BugStepSummary step) =>
        new(step.Id, step.StepNumber, step.Text, Map(step.Attachments, ToAttachment));

    private static McpComment ToComment(Comment comment) =>
        new(
            comment.Id,
            comment.Text,
            comment.CreatorUserId,
            McpWire.FormatCreatorType(comment.CreatorType),
            McpWire.FormatAudience(comment.Audience),
            comment.CreatedAt,
            Map(comment.Attachments, ToAttachment));

    private static McpAttachment ToAttachment(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.FileName,
            McpWire.FormatAttachType(attachment.AttachType),
            attachment.HasPreview ?? false);

    /// <summary>Пустая коллекция → <c>null</c>, чтобы сериализатор выбросил поле: <c>"bugs":[]</c> — шум.</summary>
    private static TResult[]? Map<TSource, TResult>(TSource[]? source, Func<TSource, TResult> map) =>
        source is null || source.Length == 0 ? null : [.. source.Select(map)];

    private static string[]? NullIfEmpty(string[]? values) =>
        values is null || values.Length == 0 ? null : values;
}
