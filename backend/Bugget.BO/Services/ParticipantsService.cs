using Bugget.DA.Interfaces;
using Bugget.DA.WebSockets;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.Services;

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
