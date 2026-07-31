using Bugget.Contracts.Dto.Link;
using Bugget.Domain.Reports;

namespace Bugget.Application.Ports;

public interface IReportLinksDbClient
{
    Task<ReportLink> CreateReportLinkInternalAsync(int reportId, ReportLinkDto dto);

    Task<ReportLink?> UpdateReportLinkInternalAsync(int reportId, int linkId, ReportLinkDto dto);

    Task<ReportLink?> DeleteReportLinkInternalAsync(int reportId, int linkId);
}
