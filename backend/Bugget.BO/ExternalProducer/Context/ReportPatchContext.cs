using Bugget.Entities.DbModels.Report;
using Bugget.Entities.DTO.Report;

namespace Bugget.BO.ExternalProducer.Context;

public record ReportPatchContext(string UserId, ReportPatchDto PatchDto, ReportPatchResultDbModel Result);
