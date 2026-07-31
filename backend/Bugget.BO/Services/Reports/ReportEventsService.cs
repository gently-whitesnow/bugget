using Bugget.BO.ExternalProducer.Context;
using Bugget.BO.Mappers;
using Bugget.BO.Ports;
using Bugget.BO.Services.External;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Report;

namespace Bugget.BO.Services.Reports;

public class ReportEventsService(
    IReportPageHubClient reportPageHubClient,
        ExternalProducerService externalProducerService,
        ParticipantsService participantsService)
{
    public async Task HandlePatchReportEventAsync(ReportIdContext reportIdContext, UserIdentity user, ReportPatchDto patchDto, ReportPatchResult result)
    {
        await Task.WhenAll(
            reportPageHubClient.SendReportPatchAsync(reportIdContext.GroupKey, patchDto.ToSocketView(result), user.SignalRConnectionId),
            externalProducerService.ExecuteReportPatchPostActions(new ReportPatchContext(user.Id, patchDto, result)),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id),
            patchDto.ResponsibleUserId != null ? participantsService.AddParticipantIfNotExistAsync(reportIdContext, patchDto.ResponsibleUserId) : Task.CompletedTask
        );
    }
}
