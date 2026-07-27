using Bugget.Entities.DbModels.Attachment;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DbModels.Comment;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.Views;
using Bugget.Entities.Views.Reports;
using Bugget.Reports.Contracts.Generated;

namespace Bugget.Mappers;

/// <summary>
/// View/DbModel → Contracts для эндпоинтов модуля reports. Контрактные DTO
/// сгенерированы из <c>specs/contracts/reports/openapi.yaml</c> и видны только
/// в проекте Bugget, поэтому маппер живёт здесь, а не в Bugget.BO.
///
/// Формы намеренно повторяют то, что уходило фронту до перехода на contract-first:
/// контракт описан с работающего API, а не наоборот. Доказательство — снимки в
/// <c>Bugget.IntegrationTests/Contract/Snapshots</c>.
///
/// Лишний хоп (DbModel → ViewModel → Contract) сохранён сознательно: те же
/// ViewModel'и уходят в SignalR-хаб, и схлопывать хопы имеет смысл вместе с
/// контрактом realtime-событий, а не в этом PR.
/// </summary>
internal static class ReportsMapper
{
    public static ReportSummary ToContract(this ReportSummaryViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = view.Status,
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Creator_user_id = view.CreatorUserId,
        Creator_team_id = view.CreatorTeamId,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        Creator_type = view.CreatorType,
    };

    public static ReportPatchResult ToContract(this ReportPatchResultViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = view.Status,
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Updated_at = view.UpdatedAt,
    };

    public static LegacyReportResolve ToContract(this LegacyReportResolveView view) => new()
    {
        Team_id = view.TeamId,
        Team_report_id = view.TeamReportId,
    };

    public static Report ToContract(this ReportViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = view.Status,
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Creator_user_id = view.CreatorUserId,
        Creator_team_id = view.CreatorTeamId,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        Creator_type = view.CreatorType,
        Is_excluded_from_analytics = view.IsExcludedFromAnalytics,
        Participants_user_ids = view.ParticipantsUserIds,
        // null здесь — часть контракта: «не запрашивали», в отличие от пустого списка.
        Links = view.Links?.Select(ToContract).ToArray(),
        Bugs = view.Bugs?.Select(ToContract).ToArray(),
    };

    public static ReportList ToContract(this ReportViews views) => new()
    {
        Total = views.Total,
        Reports = views.Reports.Select(ToContract).ToArray(),
    };

    public static ReportCountsBatchResponse ToCountsContract(this IReadOnlyDictionary<string, long> counts) => new()
    {
        Counts = counts,
    };

    public static ReportLink ToContract(this ReportLinkDbModel model) => new()
    {
        Id = model.Id,
        Report_id = model.ReportId,
        Link = model.Link,
        Name = model.Name,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static Bug ToContract(this BugDbModel model) => new()
    {
        Id = model.Id,
        Report_id = model.ReportId,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Creator_user_id = model.CreatorUserId,
        Status = model.Status,
        Creator_type = model.CreatorType,
        Attachments = model.Attachments?.Select(ToContract).ToArray(),
        Comments = model.Comments?.Select(ToContract).ToArray(),
        Steps = model.Steps?.Select(ToContract).ToArray(),
    };

    public static Comment ToContract(this CommentDbModel model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Creator_user_id = model.CreatorUserId,
        Creator_type = model.CreatorType,
        Audience = model.Audience,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Attachments = model.Attachments?.Select(ToContract).ToArray(),
    };

    public static BugSummary ToSummaryContract(this BugSummaryDbModel model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Creator_user_id = model.CreatorUserId,
        Status = model.Status,
        Creator_type = model.CreatorType,
    };

    public static BugPatchResult ToContract(this BugPatchResultDbModel model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Updated_at = model.UpdatedAt,
        Status = model.Status,
    };

    public static CommentSummary ToSummaryContract(this CommentSummaryDbModel model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Creator_user_id = model.CreatorUserId,
        Creator_type = model.CreatorType,
        Audience = model.Audience,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static BugStep ToContract(this BugStepSummaryDbModel model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Step_number = model.StepNumber,
        Creator_user_id = model.CreatorUserId,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Attachments = model.Attachments?.Select(ToContract).ToArray(),
    };

    public static Attachment ToContract(this AttachmentDbModel model) => new()
    {
        Id = model.Id,
        Entity_id = model.EntityId,
        Attach_type = model.AttachType,
        Storage_key = model.StorageKey,
        Storage_kind = model.StorageKind,
        Created_at = model.CreatedAt,
        Creator_user_id = model.CreatorUserId,
        Length_bytes = model.LengthBytes,
        File_name = model.FileName,
        Mime_type = model.MimeType,
        Has_preview = model.HasPreview,
        Is_gzip_compressed = model.IsGzipCompressed,
    };
}
