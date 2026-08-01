using Bugget.Application.ExternalProducer.Context;
using Bugget.Application.ExternalProducer.Ports;

namespace Bugget.Application.Services.External;

public sealed class ExternalProducerService(
    IEnumerable<IReportPatchPostAction> reportPatchPostActions)
{
    public async Task ExecuteReportPatchPostActions(ReportPatchContext reportPatchContext)
    {
        foreach (var reportPatchPostAction in reportPatchPostActions)
        {
            await reportPatchPostAction.ExecuteAsync(reportPatchContext);
        }
    }
}
