using Bugget.Domain.Analytics;

namespace Bugget.Application.Ports;

/// <summary>
/// Read-API над <c>report_phase_intervals</c> + <c>reports</c> для <c>/v2/analytics/*</c>. Все запросы фильтруют
/// по <c>reports.is_excluded_from_analytics = FALSE</c> и по workspace.
/// </summary>
public interface IAnalyticsDbClient
{
    /// <summary>Сырые данные summary за период from &lt;= closed_at &lt; to; teamId фильтрует по <c>reports.creator_team_id</c>.</summary>
    Task<AnalyticsRawData> GetSummaryDataAsync(
        string workspaceId,
        string? teamId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);

    /// <summary>Таймлайн фаз по <c>entered_at</c>; <c>null</c> — репорт не найден, не в workspace или исключён из аналитики.</summary>
    Task<IReadOnlyList<PhaseIntervalBo>?> GetReportTimelineAsync(
        string workspaceId,
        long reportId,
        CancellationToken ct);

    Task<BugsByStatusBo> GetBugsByStatusAsync(int reportId, CancellationToken ct);

    /// <summary>Кол-во багов, созданных во время хотя бы одного Test-интервала с <c>regression_cycle_index ≥ 1</c>.</summary>
    Task<int> GetBugsAddedDuringRegressionAsync(int reportId, CancellationToken ct);

    /// <summary>Данные для <c>GET /v2/analytics/responsible/{userId}</c>; limit 10 на каждый список.</summary>
    Task<AnalyticsResponsibleRawData> GetResponsibleDataAsync(
        string workspaceId,
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);
}
