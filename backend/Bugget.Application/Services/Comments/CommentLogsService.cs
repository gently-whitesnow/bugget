using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Bug;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Comments;
using Bugget.Domain.Common;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Comments;

public class CommentLogsService(IUsersClient usersClient, ICommentsDbClient commentsDbClient, IReportPageHubClient reportPageHubClient)
{
    public async Task LogPatchBugAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugPatchDto patchDto)
    {
        if (patchDto.Status == null)
        {
            return;
        }
        var actorUser = await usersClient.GetUserAsync(user.Id);
        if (actorUser == null)
        {
            return;
        }

        var commentText = GetCommentText(patchDto, actorUser.Name);

        var commentSummary = await commentsDbClient.CreateCommentAsync(SystemCreators.Bot, bugId, commentText, (int)CreatorType.System);
        await reportPageHubClient.SendCommentCreateAsync(reportIdContext.GroupKey, commentSummary, null);
    }

    private string GetCommentText(BugPatchDto patchDto, string actorName)
    {
        switch (patchDto.Status)
        {
            case (int)BugStatus.Open:
                return $"{actorName}: статус → Открыт";
            case (int)BugStatus.Fixed:
                return $"{actorName}: статус → Исправлен";
            case (int)BugStatus.Verified:
                return $"{actorName}: статус → Проверен";
            case (int)BugStatus.Rejected:
                return $"{actorName}: статус → Отклонен";
            default:
                throw new InvalidOperationException($"Неизвестный статус бага: {patchDto.Status}");
        }
    }
}
