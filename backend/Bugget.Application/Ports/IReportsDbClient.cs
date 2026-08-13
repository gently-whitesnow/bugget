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

    /// <summary>
    /// Tx-aware снимок репорта перед PATCH: `status`, `responsible_user_id`,
    /// `past_responsible_user_id`, `creator_user_id`. Нужен драйверам effective-патча
    /// — auto-status по смене responsible (T04) и agent-handoff по смене статуса
    /// (kaiten 238350) — в той же транзакции, что и UPDATE. Возвращает <c>null</c>,
    /// если репорт не найден.
    /// </summary>
    Task<ReportPatchSnapshot?> GetPatchSnapshotAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Tx-aware fetch текущего <c>is_excluded_from_analytics</c> репорта.
    /// Используется PATCH-эндпоинтом T11: до UPDATE проверяем, изменится ли флаг,
    /// чтобы дедуплицировать domain event <c>excluded_from_analytics_toggled</c>
    /// (TECHSPEC §4.5). Берёт <c>SELECT ... FOR UPDATE</c>, поэтому требуется scope.
    /// <c>null</c> — репорт не найден.
    /// </summary>
    Task<bool?> GetIsExcludedFromAnalyticsAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Публичная (без транзакции) выборка текущего <c>is_excluded_from_analytics</c>.
    /// Используется в нетранзакционной эмиссии <c>excluded_from_analytics_toggled</c>
    /// (TECHSPEC §4.5): pre-fetch старого значения для дедупликации не должен
    /// разделять транзакцию с PATCH-UPDATE, событие чисто аудит.
    /// <c>null</c> — репорт не найден.
    /// </summary>
    Task<bool?> GetIsExcludedFromAnalyticsAsync(
        int reportId,
        CancellationToken ct = default);

    Task<ReportListItem[]> ListByCreatorInternalAsync(
        string organizationId,
        string creatorUserId,
        short creatorType,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Обновление полей репорта. Если <paramref name="scope"/> передан — операция
    /// выполняется в его транзакции (для эмиссии domain events в той же транзакции,
    /// что и UPDATE); если <c>null</c> — клиент открывает собственное соединение.
    /// </summary>
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
