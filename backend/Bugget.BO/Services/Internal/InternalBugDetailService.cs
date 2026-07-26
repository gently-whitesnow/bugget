using Bugget.BO.Errors;
using Bugget.DA.Interfaces;
using Bugget.Entities.BO;
using Bugget.Entities.DTO.Internal;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// GET /v2/_internal/bugs/{bugId} — полная карточка bug'а для рендера
/// в Telegram-боте. См. TECHSPEC §4.3.6, BETA-BOT-UX-CARD-FULL-DATA.
/// Cross-tenant guard выполняется на стороне beta-bot через `SubmissionLog`,
/// поэтому здесь только 404 на отсутствующий bug.
/// </summary>
public sealed class InternalBugDetailService(IBugsDbClient bugsDbClient)
{
    private static readonly int[] BugAttachTypes =
        [(int)AttachType.Fact, (int)AttachType.Expected];

    public async Task<MonadeStruct<InternalBugDetailResponseDto>> GetAsync(int bugId, CancellationToken ct = default)
    {
        var row = await bugsDbClient.GetBugDetailInternalAsync(bugId, BugAttachTypes, ct);
        if (row is null)
        {
            return BoErrors.BugNotFoundError;
        }

        return new InternalBugDetailResponseDto
        {
            BugId = row.BugId,
            ReportId = row.ReportId,
            ReportNumber = row.ReportNumber,
            ReportStatus = row.ReportStatus,
            Title = row.Title,
            Status = row.Status,
            CreatorType = row.CreatorType,
            CreatorUserId = row.CreatorUserId,
            Receive = row.Receive,
            Expect = row.Expect,
            AttachmentsCount = row.AttachmentsCount,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }
}
