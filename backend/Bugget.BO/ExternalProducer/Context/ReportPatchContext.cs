using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Report;

namespace Bugget.BO.ExternalProducer.Context;

public record ReportPatchContext(string UserId, ReportPatchDto PatchDto, ReportPatchResult Result);
