using Bugget.Application.Ports;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services;

public class ParticipantsService(IParticipantsDbClient participantsDbClient, IReportPageHubClient reportPageHubClient)
{
    public async Task AddParticipantIfNotExistAsync(ReportIdContext reportIdContext, string userId)
    {
        var participants = await participantsDbClient.AddParticipantIfNotExistAsync(reportIdContext.ReportId, userId);

        if (participants != null)
        {
            await reportPageHubClient.SendNewReportParticipantAsync(reportIdContext.GroupKey, userId);
        }
    }
}
