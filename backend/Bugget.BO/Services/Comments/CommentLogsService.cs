using Bugget.BO.Ports;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.Common;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Bug;

namespace Bugget.BO.Services.Comments;

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

        var commentSummaryDbModel = await commentsDbClient.CreateCommentAsync(SystemCreators.Bot, bugId, commentText, (int)CreatorType.System);
        await reportPageHubClient.SendCommentCreateAsync(reportIdContext.GroupKey, commentSummaryDbModel, null);
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
