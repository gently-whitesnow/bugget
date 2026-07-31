using System.ComponentModel.DataAnnotations;
namespace Bugget.Contracts.Dto.Report;

public sealed class ReportPatchDto
{
    [StringLength(128, MinimumLength = 1)]
    public string? Title { get; init; }
    // Диапазон совпадает со всеми значениями `ReportStatus` (T01):
    // Backlog=0, Resolved=1, Fix=2, Rejected=3, Test=4. Test нужен,
    // чтобы manual override через PATCH мог явно перевести репорт в Test.
    [Range(0, 4)]
    public int? Status { get; init; }
    [StringLength(256, MinimumLength = 1)]
    public string? ResponsibleUserId { get; init; }
    // T11 · TECHSPEC §4.5. NULL — не менять значение (consistent с остальными
    // полями PATCH). При фактическом изменении ReportsService.PatchReportAsync
    // эмитит domain event `bugget.report.excluded_from_analytics_toggled`.
    public bool? IsExcludedFromAnalytics { get; init; }
}
