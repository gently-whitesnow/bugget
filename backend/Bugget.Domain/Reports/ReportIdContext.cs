namespace Bugget.Domain.Reports;

public record struct ReportIdContext(int ReportId, string AliasId, string? TeamId)
{
    public string GroupKey => string.IsNullOrEmpty(TeamId) ? AliasId : $"{TeamId}:{AliasId}";
}

public sealed class ResolvedReportId
{
    public required int Id { get; init; }
    public string? CreatorTeamId { get; init; }
    public int? TeamReportId { get; init; }
}
