namespace Bugget.Entities.DTO.Internal;

public sealed class InternalReportListItemDto
{
    public required int ReportId { get; init; }
    public int? ReportNumber { get; init; }
    public string? ReportTitle { get; init; }
    public required int ReportStatus { get; init; }
    public required InternalReportBugDto Bug { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
}

public sealed class InternalReportBugDto
{
    public required int BugId { get; init; }
    public string? Title { get; init; }
    public required int Status { get; init; }
}
