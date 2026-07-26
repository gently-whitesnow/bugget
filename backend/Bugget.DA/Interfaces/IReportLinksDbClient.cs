using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Link;

namespace Bugget.DA.Interfaces;

public interface IReportLinksDbClient
{
    Task<ReportLinkDbModel> CreateReportLinkInternalAsync(int reportId, ReportLinkDto dto);

    Task<ReportLinkDbModel?> UpdateReportLinkInternalAsync(int reportId, int linkId, ReportLinkDto dto);

    Task<ReportLinkDbModel?> DeleteReportLinkInternalAsync(int reportId, int linkId);
}
