using Bugget.Application.ExternalProducer.Context;

namespace Bugget.Application.ExternalProducer.Ports;

public interface IReportPatchPostAction
{
    public Task ExecuteAsync(ReportPatchContext reportPatchContext);
}
