using Bugget.Application.Commands.Report;
using Bugget.Domain;
using Bugget.Domain.Reports;
using Bugget.Domain.Search;

namespace Bugget.Application.Ports;

public interface IReportsDbClient
{
    Task<Report?> GetReportInternalAsync(int reportId);

    Task<(long total, Report[] reports)> ListReportsAsync(
        string? organizationId,
        string? userId,
        string? teamId,
        int[]? statuses,
        int[]? creatorTypes,
        int skip,
        int take);

    Task<ReportSummary> CreateReportAsync(
        string userId, string? teamId, string? organizationId, ReportCreateDto dto,
        short creatorType = (short)Bugget.Domain.Common.CreatorType.User);

    Task<ReportSummary> CreateReportAsync(
        ITransactionScope scope,
        string userId,
        string? teamId,
        string? organizationId,
        string? title,
        short creatorType);

    Task<int?> GetStatusInternalAsync(ITransactionScope scope, int reportId, CancellationToken ct = default);

    /// <summary>Снимок репорта перед PATCH в той же транзакции, что и UPDATE (auto-status, agent-handoff); <c>null</c> — не найден.</summary>
    Task<ReportPatchSnapshot?> GetPatchSnapshotAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default);

    /// <summary>Берёт <c>SELECT ... FOR UPDATE</c> для дедупликации <c>excluded_from_analytics_toggled</c> (TECHSPEC §4.5); <c>null</c> — не найден.</summary>
    Task<bool?> GetIsExcludedFromAnalyticsAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default);

    /// <summary>Вариант без транзакции: событие — чистый аудит и не должно делить транзакцию с PATCH-UPDATE.</summary>
    Task<bool?> GetIsExcludedFromAnalyticsAsync(
        int reportId,
        CancellationToken ct = default);

    Task<ReportListItem[]> ListByCreatorInternalAsync(
        string organizationId,
        string creatorUserId,
        short creatorType,
        int limit,
        CancellationToken ct = default);

    /// <summary>Со <paramref name="scope"/> идёт в его транзакции (domain events вместе с UPDATE); без него — своё соединение.</summary>
    Task<ReportPatchResult> PatchReportAsync(
        int reportId,
        ReportPatchDto dto,
        ITransactionScope? scope = null,
        CancellationToken ct = default);

    Task<(long total, Report[] reports)> SearchReportsAsync(SearchReports search);

    Task<long> CountReportsAsync(
        string? organizationId,
        string? teamId,
        int[]? statuses,
        short[]? creatorTypes,
        CancellationToken ct = default);

    Task ChangeStatusAsync(int reportId, int newStatus);

    Task ChangeStatusAsync(
        ITransactionScope scope,
        int reportId,
        int newStatus,
        CancellationToken ct = default);

    Task<ResolvedReportId?> ResolveReportIdAsync(
        string? workspaceId,
        string? teamId,
        int? reportId,
        Guid? publicId,
        int? teamReportId);
}
