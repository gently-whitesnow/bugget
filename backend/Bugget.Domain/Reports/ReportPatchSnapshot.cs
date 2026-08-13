namespace Bugget.Domain.Reports;

/// <summary>
/// Снимок строки репорта перед PATCH — всё, что нужно драйверам effective-патча
/// (auto-status по смене responsible и <see cref="AgentReportHandoff"/> по смене
/// статуса) в той же транзакции, что и UPDATE.
/// </summary>
public sealed record ReportPatchSnapshot(
    int Status,
    string? ResponsibleUserId,
    string? PastResponsibleUserId,
    string CreatorUserId);
