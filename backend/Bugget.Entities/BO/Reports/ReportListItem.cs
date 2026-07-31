namespace Bugget.Entities.BO.ReportBo;

public sealed class ReportListItem
{
    public required int ReportId { get; init; }
    public int? ReportNumber { get; init; }
    public string? ReportTitle { get; init; }
    public required int ReportStatus { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    public required int BugId { get; init; }
    public string? BugTitle { get; init; }
    public required int BugStatus { get; init; }
}
