using Bugget.Domain.Analytics;

namespace Bugget.Application.Services.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsSummaryBo> GetSummaryAsync(string workspaceId, string? period, string? teamId, CancellationToken ct);
    Task<AnalyticsResponsibleBo> GetByResponsibleAsync(string workspaceId, string userId, string? period, CancellationToken ct);
    Task<AnalyticsReportBo?> GetReportAsync(string workspaceId, long reportId, CancellationToken ct);
}
