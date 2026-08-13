using Bugget.Application.ExternalProducer.Context;
using Bugget.Application.ExternalProducer.Ports;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Common;
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
        if (responsibleUser?.MattermostUserId is null)
        {
            return;
        }

        var initiatorName = await ResolveInitiatorNameAsync(reportPatchContext);
        if (initiatorName == null)
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
            aliasId, teamId, reportPatchContext.Result.Title, initiatorName
        );

        await mattermostClient.SendMessageAsync(responsibleUser.MattermostUserId, message);
    }

    /// <summary>
    /// Кем подписать уведомление. У агента имени нет — <c>UserId</c> указывает на
    /// владельца токена, и подставлять его имя нельзя: репорт перевёл агент.
    /// <c>null</c> — инициатор-человек не нашёлся, отправлять нечего.
    /// </summary>
    private async Task<string?> ResolveInitiatorNameAsync(ReportPatchContext reportPatchContext)
    {
        if (reportPatchContext.ActorCreatorType == CreatorType.Agent)
        {
            return ReportMessageBuilder.AgentInitiatorName;
        }

        var updaterUser = await usersClient.GetUserAsync(reportPatchContext.UserId);
        return updaterUser?.Name;
    }
}
