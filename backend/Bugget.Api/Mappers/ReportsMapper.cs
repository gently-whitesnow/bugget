using Bugget.Api.Http;
using Bugget.Application.Results;
using Bugget.Application.Results.Reports;
using Bugget.Contracts.Reports.Generated;
using DomainModel = Bugget.Domain;

namespace Bugget.Api.Mappers;

/// <summary>
/// Домен/View → Contracts для эндпоинтов модуля reports. Контрактные DTO
/// сгенерированы из <c>specs/contracts/reports/openapi.yaml</c> и видны только
/// в проекте Bugget, поэтому маппер живёт здесь, а не в Bugget.Api.BO.
///
/// Формы намеренно повторяют то, что уходило фронту до перехода на contract-first:
/// контракт описан с работающего API, а не наоборот. Доказательство — снимки в
/// <c>Bugget.IntegrationTests/Contract/Snapshots</c>. Единственное осознанное сужение —
/// вложения и элемент списка репортов (ADR-0005, «Сужение wire-контракта reports»).
///
/// Лишний хоп (доменная модель → ViewModel → Contract) сохранён сознательно: те же
/// ViewModel'и уходят в SignalR-хаб, и схлопывать хопы имеет смысл вместе с
/// контрактом realtime-событий, а не в этом PR.
/// </summary>
internal static class ReportsMapper
{
    public static ReportSummary ToContract(this ReportSummaryViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = WireEnumMapper.ToReportStatusWire(view.Status),
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Creator_user_id = view.CreatorUserId,
        Creator_team_id = view.CreatorTeamId,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        Creator_type = WireEnumMapper.ToCreatorTypeWire(view.CreatorType),
    };

    public static ReportPatchResult ToContract(this ReportPatchResultViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = WireEnumMapper.ToReportStatusWire(view.Status),
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
        Status = WireEnumMapper.ToReportStatusWire(view.Status),
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Creator_user_id = view.CreatorUserId,
        Creator_team_id = view.CreatorTeamId,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        Creator_type = WireEnumMapper.ToCreatorTypeWire(view.CreatorType),
        Is_excluded_from_analytics = view.IsExcludedFromAnalytics,
        Participants_user_ids = view.ParticipantsUserIds,
        // null здесь — часть контракта: «не запрашивали», в отличие от пустого списка.
        Links = view.Links?.Select(ToContract).ToArray(),
        Bugs = view.Bugs?.Select(ToContract).ToArray(),
    };

    /// <remarks>
    /// LIST отдаёт свою форму, а не <see cref="Report"/>: ссылки, вложения багов
    /// и шаги воспроизведения список не загружает (см. <c>list_reports_internal</c>
    /// и соседние запросы в <c>ReportsDbClient</c>), и раньше они уходили наружу
    /// только как `null`. Причина и последствия — ADR-0005.
    /// </remarks>
    public static ReportListItem ToListContract(this ReportViewModel view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Status = WireEnumMapper.ToReportStatusWire(view.Status),
        Responsible_user_id = view.ResponsibleUserId,
        Past_responsible_user_id = view.PastResponsibleUserId,
        Creator_user_id = view.CreatorUserId,
        Creator_team_id = view.CreatorTeamId,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        Creator_type = WireEnumMapper.ToCreatorTypeWire(view.CreatorType),
        Is_excluded_from_analytics = view.IsExcludedFromAnalytics,
        Participants_user_ids = view.ParticipantsUserIds,
        // null здесь — часть контракта: «не запрашивали», в отличие от пустого списка.
        Bugs = view.Bugs?.Select(ToListContract).ToArray(),
    };

    public static BugListItem ToListContract(this DomainModel.Bugs.Bug model) => new()
    {
        Id = model.Id,
        Report_id = model.ReportId,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Creator_user_id = model.CreatorUserId,
        Status = WireEnumMapper.ToBugStatusWire(model.Status),
        Creator_type = WireEnumMapper.ToCreatorTypeWire(model.CreatorType),
        Comments = model.Comments?.Select(ToContract).ToArray(),
    };

    public static ReportList ToContract(this ReportViews views) => new()
    {
        // `total` и `count` уходят каноническим Int64 строкой (shared.yaml
        // `Int64String`): у единственного клиента JSON-число — double.
        Total = WireInt64.ToWire(views.Total),
        Reports = views.Reports.Select(ToListContract).ToArray(),
    };

    /// <summary>
    /// Счётчики уходят массивом в порядке срезов запроса: ключ среза задаёт клиент,
    /// и в объекте со свободными ключами он был бы неотличим от имени поля (ADR-0009).
    /// </summary>
    public static ReportCountsBatchResponse ToCountsContract(this IEnumerable<KeyValuePair<string, long>> counts) => new()
    {
        Counts = counts
            .Select(pair => new ReportCountsItem { Key = pair.Key, Count = WireInt64.ToWire(pair.Value) })
            .ToArray(),
    };

    public static ReportLink ToContract(this DomainModel.Reports.ReportLink model) => new()
    {
        Id = model.Id,
        Report_id = model.ReportId,
        Link = model.Link,
        Name = model.Name,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static Bug ToContract(this DomainModel.Bugs.Bug model) => new()
    {
        Id = model.Id,
        Report_id = model.ReportId,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Creator_user_id = model.CreatorUserId,
        Status = WireEnumMapper.ToBugStatusWire(model.Status),
        Creator_type = WireEnumMapper.ToCreatorTypeWire(model.CreatorType),
        Attachments = model.Attachments?.Select(ToSummaryContract).ToArray(),
        Comments = model.Comments?.Select(ToContract).ToArray(),
        Steps = model.Steps?.Select(ToContract).ToArray(),
    };

    public static Comment ToContract(this DomainModel.Comments.Comment model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Creator_user_id = model.CreatorUserId,
        Creator_type = WireEnumMapper.ToCreatorTypeWire(model.CreatorType),
        Audience = WireEnumMapper.ToCommentAudienceWire(model.Audience),
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Attachments = model.Attachments?.Select(ToSummaryContract).ToArray(),
    };

    public static BugSummary ToSummaryContract(this DomainModel.Bugs.BugSummary model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Creator_user_id = model.CreatorUserId,
        Status = WireEnumMapper.ToBugStatusWire(model.Status),
        Creator_type = WireEnumMapper.ToCreatorTypeWire(model.CreatorType),
    };

    public static BugPatchResult ToContract(this DomainModel.Bugs.BugPatchResult model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Receive = model.Receive,
        Expect = model.Expect,
        Updated_at = model.UpdatedAt,
        Status = WireEnumMapper.ToBugStatusWire(model.Status),
    };

    public static CommentSummary ToSummaryContract(this DomainModel.Comments.CommentSummary model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Creator_user_id = model.CreatorUserId,
        Creator_type = WireEnumMapper.ToCreatorTypeWire(model.CreatorType),
        Audience = WireEnumMapper.ToCommentAudienceWire(model.Audience),
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static BugStep ToContract(this DomainModel.Bugs.BugStepSummary model) => new()
    {
        Id = model.Id,
        Bug_id = model.BugId,
        Text = model.Text,
        Step_number = model.StepNumber,
        Creator_user_id = model.CreatorUserId,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
        Attachments = model.Attachments?.Select(ToSummaryContract).ToArray(),
    };

    /// <remarks>
    /// Единственная публичная форма вложения в модуле: и ручки загрузки и
    /// переименования, и вложения внутри репорта отдают её. Поля хранилища
    /// (`storage_key`, `storage_kind`, `length_bytes`, `mime_type`,
    /// `is_gzip_compressed`) наружу не уходят — причина в ADR-0005.
    /// </remarks>
    public static AttachmentSummary ToSummaryContract(this DomainModel.Attachments.Attachment model) => new()
    {
        Id = model.Id,
        Entity_id = model.EntityId,
        Attach_type = WireEnumMapper.ToAttachTypeWire(model.AttachType),
        Created_at = model.CreatedAt,
        Creator_user_id = model.CreatorUserId,
        File_name = model.FileName,
        Has_preview = model.HasPreview == true,
    };
}
