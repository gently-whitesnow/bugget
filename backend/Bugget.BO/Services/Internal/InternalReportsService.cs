using Bugget.BO.Errors;
using Bugget.DA.Interfaces;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Internal;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// GET /v2/_internal/reports — список репортов тестера для команды <c>/my</c>.
/// I-11: возвращается только Bug, созданный этим <c>creatorUserId</c>+<c>TgBetaTester</c>;
/// Bug'и, добавленные командой постфактум в тот же Report, в ответ не попадают.
/// См. TECHSPEC §4.3.5.
/// </summary>
public sealed class InternalReportsService(IReportsDbClient reportsDbClient)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;
    private const short CreatorTypeTgBetaTester = (short)CreatorType.TgBetaTester;

    public async Task<MonadeStruct<InternalReportsListResponseDto>> ListAsync(
        string? workspaceId,
        string? creatorUserId,
        int? limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BoErrors.WorkspaceIdRequired;
        }

        if (string.IsNullOrWhiteSpace(creatorUserId))
        {
            return BoErrors.CreatorUserIdRequired;
        }

        var resolvedLimit = limit is null
            ? DefaultLimit
            : Math.Clamp(limit.Value, 1, MaxLimit);

        var rows = await reportsDbClient.ListByCreatorInternalAsync(
            workspaceId,
            creatorUserId,
            CreatorTypeTgBetaTester,
            resolvedLimit,
            ct);

        var items = rows.Select(r => new InternalReportListItemDto
        {
            ReportId = r.ReportId,
            ReportNumber = r.ReportNumber,
            ReportTitle = r.ReportTitle,
            ReportStatus = r.ReportStatus,
            SubmittedAt = r.SubmittedAt,
            Bug = new InternalReportBugDto
            {
                BugId = r.BugId,
                Title = r.BugTitle,
                Status = r.BugStatus,
            },
        }).ToArray();

        return new InternalReportsListResponseDto { Items = items };
    }
}
