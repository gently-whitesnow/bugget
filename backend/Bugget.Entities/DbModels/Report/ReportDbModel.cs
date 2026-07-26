using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DbModels.ReportLink;

namespace Bugget.Entities.DbModels.Report;

public sealed class ReportDbModel
{
    public required int Id { get; init; }
    public int? TeamReportId { get; init; }
    public required Guid PublicId { get; init; }
    public required string Title { get; init; }
    public required int Status { get; init; }
    public required string ResponsibleUserId { get; init; }
    public required string PastResponsibleUserId { get; init; }
    public required string CreatorUserId { get; init; }
    public string? CreatorTeamId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required int CreatorType { get; init; }
    public required bool IsExcludedFromAnalytics { get; init; }
    public required string[] ParticipantsUserIds { get; set; }
    public ReportLinkDbModel[]? Links { get; set; }
    public BugDbModel[]? Bugs { get; set; }
}
