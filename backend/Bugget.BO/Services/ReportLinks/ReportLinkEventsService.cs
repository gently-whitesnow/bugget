using Bugget.BO.Ports;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.Services.ReportLinks;

public sealed class ReportLinkEventsService(IReportPageHubClient reportPageHubClient)
{
    public Task HandleReportLinkCreateAsync(ReportIdContext reportIdContext, UserIdentity user, ReportLink linkDbModel)
    {
        return reportPageHubClient.SendReportLinkCreateAsync(reportIdContext.GroupKey, linkDbModel, user.SignalRConnectionId);
    }

    public Task HandleReportLinkUpdateAsync(ReportIdContext reportIdContext, UserIdentity user, ReportLink linkDbModel)
    {
        return reportPageHubClient.SendReportLinkUpdateAsync(reportIdContext.GroupKey, linkDbModel, user.SignalRConnectionId);
    }

    public Task HandleReportLinkDeleteAsync(ReportIdContext reportIdContext, UserIdentity user, int linkId)
    {
        return reportPageHubClient.SendReportLinkDeleteAsync(reportIdContext.GroupKey, linkId, user.SignalRConnectionId);
    }
}
