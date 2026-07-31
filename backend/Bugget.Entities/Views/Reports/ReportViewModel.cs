using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.Entities.Views.Reports;

public sealed class ReportViewModel
{
    public required string Id { get; init; }
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
    public ReportLink[]? Links { get; set; }
    public Bug[]? Bugs { get; set; }
}
