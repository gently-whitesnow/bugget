using Bugget.Application.ExternalProducer.Context;

namespace Bugget.Application.ExternalProducer.Interfaces;

public interface IReportPatchPostAction
{
    public Task ExecuteAsync(ReportPatchContext reportPatchContext);
}
