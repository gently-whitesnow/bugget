using Bugget.Contracts.Dto.Report;
using Bugget.Domain.Reports;

namespace Bugget.Application.ExternalProducer.Context;

public record ReportPatchContext(string UserId, ReportPatchDto PatchDto, ReportPatchResult Result);
