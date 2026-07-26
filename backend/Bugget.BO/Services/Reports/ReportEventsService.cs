using Bugget.BO.ExternalProducer.Context;
using Bugget.BO.Mappers;
using Bugget.BO.Services.External;
using Bugget.DA.WebSockets;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.Report;
using Bugget.Entities.DTO.Report;

namespace Bugget.BO.Services.Reports;

public class ReportEventsService(
    IReportPageHubClient reportPageHubClient,
        ExternalProducerService externalProducerService,
        ParticipantsService participantsService)
{
    public async Task HandlePatchReportEventAsync(ReportIdContext reportIdContext, UserIdentity user, ReportPatchDto patchDto, ReportPatchResultDbModel result)
    {
        await Task.WhenAll(
            reportPageHubClient.SendReportPatchAsync(reportIdContext.GroupKey, patchDto.ToSocketView(result), user.SignalRConnectionId),
            externalProducerService.ExecuteReportPatchPostActions(new ReportPatchContext(user.Id, patchDto, result)),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id),
            patchDto.ResponsibleUserId != null ? participantsService.AddParticipantIfNotExistAsync(reportIdContext, patchDto.ResponsibleUserId) : Task.CompletedTask
        );
    }
}
