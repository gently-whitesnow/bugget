using Bugget.BO.ExternalProducer.Context;

namespace Bugget.BO.ExternalProducer.Interfaces;

public interface IReportPatchPostAction
{
    public Task ExecuteAsync(ReportPatchContext reportPatchContext);
}
