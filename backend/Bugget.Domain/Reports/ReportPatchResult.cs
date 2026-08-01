namespace Bugget.Domain.Reports;

public sealed class ReportPatchResult
{
    public required int Id { get; init; }
    public int? TeamReportId { get; init; }
    public required Guid PublicId { get; init; }
    public required string Title { get; init; }
    public required int Status { get; init; }
    public required string ResponsibleUserId { get; init; }
    public required string PastResponsibleUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public string? CreatorTeamId { get; init; }
    // T11: возвращается из patch_report_internal, нужно BO-слою для логирования /
    // диагностики. Само решение «эмитить или нет» бизнес-событие принимается до
    // UPDATE — на pre-fetch'е, см. ReportsService.PatchReportAsync.
    public bool IsExcludedFromAnalytics { get; init; }
}
