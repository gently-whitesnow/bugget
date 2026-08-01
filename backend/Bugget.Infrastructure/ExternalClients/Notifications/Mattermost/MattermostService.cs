using Bugget.Application.ExternalProducer.Context;
using Bugget.Application.ExternalProducer.Ports;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.ExternalClients.Notifications.Mattermost;

public sealed class MattermostService(
    IUsersClient usersClient,
    MattermostClient mattermostClient,
    IOptions<ReportAliasOptions> reportAliasOptions) : IReportPatchPostAction
{
    public async Task ExecuteAsync(ReportPatchContext reportPatchContext)
    {
        // не меняли ответственного
        if (reportPatchContext.PatchDto.ResponsibleUserId == null)
        {
            return;
        }

        if (reportPatchContext.UserId == reportPatchContext.Result.ResponsibleUserId)
        {
            return;
        }

        var responsibleUser = await usersClient.GetUserAsync(reportPatchContext.Result.ResponsibleUserId);
        var updaterUser = await usersClient.GetUserAsync(reportPatchContext.UserId);
        if (responsibleUser == null || updaterUser == null || responsibleUser.MattermostUserId is null)
        {
            return;
        }

        var aliasId = ReportIdResolveHelper.ToAliasId(
            reportPatchContext.Result.Id,
            reportPatchContext.Result.PublicId,
            reportPatchContext.Result.TeamReportId,
            reportAliasOptions.Value
        );

        var teamId = reportAliasOptions.Value.AliasMode == ReportAliasMode.Team
            ? reportPatchContext.Result.CreatorTeamId
            : null;

        var message = ReportMessageBuilder.GetYourResponsibleAfterPatchReportMessage(
            aliasId, teamId, reportPatchContext.Result.Title, updaterUser.Name
        );

        await mattermostClient.SendMessageAsync(responsibleUser.MattermostUserId, message);
    }
}
