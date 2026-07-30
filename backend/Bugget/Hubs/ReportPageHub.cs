using Bugget.BO.Errors;
using Bugget.BO.Services;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.Options;
using Bugget.Extensions;
using Bugget.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
namespace Bugget.Hubs;

public sealed class ReportPageHub(
    ILogger<ReportPageHub> logger,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions) : Hub
{
    // Подключение к группе комментариев по reportId
    public async Task JoinReportGroupAsync(string aliasId)
    {
        var user = (Context.User?.GetIdentity()) ?? throw new RealtimeProblemException(CommonProblemDescriptors.Unauthorized);
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        ) ?? throw new RealtimeProblemException(BoErrors.ReportNotFoundError.ToDescriptor());
        var groupKey = new Bugget.Entities.BO.ReportBo.ReportIdContext(
            resolvedReport.Id,
            aliasId,
            resolvedReport.CreatorTeamId
        ).GroupKey;

        logger.LogWarning("Клиент {@ConnectionId} подключился к группе {@ReportId}", Context.ConnectionId, groupKey);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey);
    }

    // Отключение от группы
    public async Task LeaveReportGroupAsync(string aliasId)
    {
        var user = (Context.User?.GetIdentity()) ?? throw new RealtimeProblemException(CommonProblemDescriptors.Unauthorized);
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );

        var groupKey = resolvedReport == null
            ? aliasId
            : new Bugget.Entities.BO.ReportBo.ReportIdContext(
                resolvedReport.Id,
                aliasId,
                resolvedReport.CreatorTeamId
            ).GroupKey;

        logger.LogWarning("Клиент {@ConnectionId} покинул группу {@ReportId}", Context.ConnectionId, groupKey);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupKey);
    }

    // Логирование разрыва соединения
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogWarning("Клиент {@ConnectionId} отключился. Причина: {@Reason}", Context.ConnectionId, exception?.Message ?? "неизвестно");
        await base.OnDisconnectedAsync(exception);
    }
}
