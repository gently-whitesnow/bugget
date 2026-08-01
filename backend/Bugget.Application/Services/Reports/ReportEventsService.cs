using Bugget.Application.Commands.Report;
using Bugget.Application.ExternalProducer.Context;
using Bugget.Application.Mappers;
using Bugget.Application.Ports;
using Bugget.Application.Services.External;
using Bugget.Domain.Authentication;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Reports;

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
