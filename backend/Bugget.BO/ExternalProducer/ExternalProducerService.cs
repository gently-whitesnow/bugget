using Bugget.BO.ExternalProducer.Context;
using Bugget.BO.ExternalProducer.Interfaces;

namespace Bugget.BO.Services.External;

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
