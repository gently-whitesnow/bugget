using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Link;

namespace Bugget.BO.Ports;

public interface IReportLinksDbClient
{
    Task<ReportLink> CreateReportLinkInternalAsync(int reportId, ReportLinkDto dto);

    Task<ReportLink?> UpdateReportLinkInternalAsync(int reportId, int linkId, ReportLinkDto dto);

    Task<ReportLink?> DeleteReportLinkInternalAsync(int reportId, int linkId);
}
