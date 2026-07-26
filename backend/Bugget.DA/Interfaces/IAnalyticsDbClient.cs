using Bugget.Entities.BO.Analytics;

namespace Bugget.DA.Interfaces;

/// <summary>
/// Read-API над <c>report_phase_intervals</c> + <c>reports</c> для эндпоинтов
/// <c>/v2/analytics/*</c>. Все запросы фильтруют по
/// <c>reports.is_excluded_from_analytics = FALSE</c> и по workspace.
/// </summary>
public interface IAnalyticsDbClient
{
    /// <summary>
    /// Собирает «сырые» данные для summary за период
    /// <paramref name="from"/> &lt;= closed_at &lt; <paramref name="to"/>.
    /// <paramref name="teamId"/> — если задан, репорты дополнительно фильтруются
    /// по <c>reports.creator_team_id</c>.
    /// </summary>
    Task<AnalyticsRawData> GetSummaryDataAsync(
        string workspaceId,
        string? teamId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);

    /// <summary>
    /// Возвращает таймлайн фаз репорта (упорядоченный по <c>entered_at</c>)
    /// с guard'ом по workspace и <c>is_excluded_from_analytics = FALSE</c>.
    /// <c>null</c> — репорт не найден / не в workspace / исключён.
    /// </summary>
    Task<IReadOnlyList<PhaseIntervalBo>?> GetReportTimelineAsync(
        string workspaceId,
        long reportId,
        CancellationToken ct);

    /// <summary>
    /// Снимок распределения багов репорта по статусам (без фильтра по периоду).
    /// </summary>
    Task<BugsByStatusBo> GetBugsByStatusAsync(int reportId, CancellationToken ct);

    /// <summary>
    /// Кол-во багов, созданных во время хотя бы одного Test-интервала с
    /// <c>regression_cycle_index ≥ 1</c>.
    /// </summary>
    Task<int> GetBugsAddedDuringRegressionAsync(int reportId, CancellationToken ct);

    /// <summary>
    /// Собирает данные для <c>GET /v2/analytics/responsible/{userId}</c>:
    /// participated + completed + avgFixPhaseDays. Limit 10 на каждый список.
    /// </summary>
    Task<AnalyticsResponsibleRawData> GetResponsibleDataAsync(
        string workspaceId,
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);
}
