using Bugget.Application.Ports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.ReportLinks;

public sealed class ReportLinkEventsService(IReportPageHubClient reportPageHubClient)
{
    public Task HandleReportLinkCreateAsync(ReportIdContext reportIdContext, UserIdentity user, ReportLink link)
    {
        return reportPageHubClient.SendReportLinkCreateAsync(reportIdContext.GroupKey, link, user.SignalRConnectionId);
    }

    public Task HandleReportLinkUpdateAsync(ReportIdContext reportIdContext, UserIdentity user, ReportLink link)
    {
        return reportPageHubClient.SendReportLinkUpdateAsync(reportIdContext.GroupKey, link, user.SignalRConnectionId);
    }

    public Task HandleReportLinkDeleteAsync(ReportIdContext reportIdContext, UserIdentity user, int linkId)
    {
        return reportPageHubClient.SendReportLinkDeleteAsync(reportIdContext.GroupKey, linkId, user.SignalRConnectionId);
    }
}
