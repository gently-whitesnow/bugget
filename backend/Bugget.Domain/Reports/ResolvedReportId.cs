namespace Bugget.Domain.Reports;

public sealed class ResolvedReportId
{
    public required int Id { get; init; }
    public string? CreatorTeamId { get; init; }
    public int? TeamReportId { get; init; }
}
