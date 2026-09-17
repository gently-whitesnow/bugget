namespace Bugget.Domain.Reports;

public record struct ReportIdContext(int ReportId, string AliasId, string? TeamId)
{
    public string GroupKey => string.IsNullOrEmpty(TeamId) ? AliasId : $"{TeamId}:{AliasId}";
}
