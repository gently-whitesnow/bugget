using Bugget.Application.Commands.Report;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Bugget.Domain.Search;

namespace Bugget.Application.Services.Reports;

public interface IReportsService
{
    Task<ReportSummary> CreateReportAsync(string userId, string? teamId, string? organizationId, ReportCreateDto createDto);
    Task<(ReportPatchResult? Value, Error? Error)> PatchReportAsync(string aliasId, UserIdentity user, ReportPatchDto patchDto);
    Task<(Report? Value, Error? Error)> GetReportAsync(string aliasId, string? organizationId, string? teamId);
    Task<ResolvedReportId?> ResolveReportByAliasAsync(string aliasId, UserIdentity user);
    Task<ResolvedReportId?> ResolveReportIdAsync(string? organizationId, string? teamId, int? reportId, Guid? publicId, int? teamReportId);
    Task<(long Total, Report[] Reports)> ListReportsAsync(string? organizationId, string? userId, string? teamId, int[]? reportStatuses, int[]? creatorTypes, int skip, int take);
    Task<(long Total, Report[] Reports)> SearchReportsAsync(SearchReports search);
    Task<long[]> CountReportsBatchAsync(string? organizationId, IReadOnlyList<ReportCountsScopeDto> scopes, CancellationToken ct = default);
}
