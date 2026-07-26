using Bugget.Entities.Options;

namespace Bugget.BO.Services.Reports;

public sealed class ReportIdResolveHelper
{
    public static (int? reportId, Guid? publicId, int? teamReportId) ResolveReportId(string aliasId, ReportAliasOptions aliasOptions)
    {
        if (aliasOptions.AliasMode == ReportAliasMode.Default)
        {
            if (int.TryParse(aliasId, out var reportId))
            {
                return (reportId, null, null);
            }
            else
            {
                return (null, null, null);
            }
        }
        else if (aliasOptions.AliasMode == ReportAliasMode.Guid)
        {
            if (Guid.TryParse(aliasId, out var publicId))
            {
                return (null, publicId, null);
            }
            else
            {
                return (null, null, null);
            }
        }
        else if (aliasOptions.AliasMode == ReportAliasMode.Team)
        {
            if (int.TryParse(aliasId, out var teamReportId))
            {
                return (null, null, teamReportId);
            }
            else
            {
                return (null, null, null);
            }
        }
        return (null, null, null);
    }

    public static string ToAliasId(int reportId, Guid publicId, int? teamReportId, ReportAliasOptions aliasOptions)
    {
        if (aliasOptions.AliasMode == ReportAliasMode.Team && teamReportId.HasValue)
        {
            return teamReportId.Value.ToString();
        }

        return aliasOptions.AliasMode == ReportAliasMode.Guid
            ? publicId.ToString()
            : reportId.ToString();
    }
}
