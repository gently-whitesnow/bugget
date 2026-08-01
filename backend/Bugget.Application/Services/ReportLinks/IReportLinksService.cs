using Bugget.Application.Commands.Link;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.ReportLinks;

public interface IReportLinksService
{
    Task<(ReportLink? Value, Error? Error)> CreateReportLinkAsync(UserIdentity user, string aliasId, ReportLinkDto dto);
    Task<(ReportLink? Value, Error? Error)> CreateReportLinkInternalAsync(UserIdentity user, ReportIdContext reportIdContext, ReportLinkDto dto);
    Task<Error?> DeleteReportLinkAsync(UserIdentity user, string aliasId, int linkId);
    Task<(ReportLink? Value, Error? Error)> UpdateReportLinkAsync(UserIdentity user, string aliasId, int linkId, ReportLinkDto dto);
}
