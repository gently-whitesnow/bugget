using Bugget.Application.ExternalProducer.Context;

namespace Bugget.Application.ExternalProducer.Ports;

public interface IReportPatchPostAction
{
    Task ExecuteAsync(ReportPatchContext reportPatchContext);
}
