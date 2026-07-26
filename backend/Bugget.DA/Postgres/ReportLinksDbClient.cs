using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Link;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class ReportLinksDbClient : PostgresClient, IReportLinksDbClient
{
    public async Task<ReportLinkDbModel> CreateReportLinkInternalAsync(int reportId, ReportLinkDto dto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<ReportLinkDbModel>(
            "SELECT * FROM public.create_report_link_internal(@report_id, @link, @name);",
            new
            {
                report_id = reportId,
                link = dto.Link,
                name = dto.Name
            }
        );
    }

    public async Task<ReportLinkDbModel?> UpdateReportLinkInternalAsync(int reportId, int linkId, ReportLinkDto dto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<ReportLinkDbModel>(
            "SELECT * FROM public.update_report_link_internal(@report_id, @link_id, @link, @name);",
            new
            {
                report_id = reportId,
                link_id = linkId,
                link = dto.Link,
                name = dto.Name
            }
        );
    }

    public async Task<ReportLinkDbModel?> DeleteReportLinkInternalAsync(int reportId, int linkId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<ReportLinkDbModel>(
            "SELECT * FROM public.delete_report_link_internal(@report_id, @link_id);",
            new
            {
                report_id = reportId,
                link_id = linkId
            }
        );
    }
}
