using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Link;
using Bugget.Domain.Reports;
using Dapper;

namespace Bugget.Infrastructure.Postgres;

public sealed class ReportLinksDbClient : PostgresClient, IReportLinksDbClient
{
    public async Task<ReportLink> CreateReportLinkInternalAsync(int reportId, ReportLinkDto dto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<ReportLink>(
            "SELECT * FROM public.create_report_link_internal(@report_id, @link, @name);",
            new
            {
                report_id = reportId,
                link = dto.Link,
                name = dto.Name
            }
        );
    }

    public async Task<ReportLink?> UpdateReportLinkInternalAsync(int reportId, int linkId, ReportLinkDto dto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<ReportLink>(
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

    public async Task<ReportLink?> DeleteReportLinkInternalAsync(int reportId, int linkId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<ReportLink>(
            "SELECT * FROM public.delete_report_link_internal(@report_id, @link_id);",
            new
            {
                report_id = reportId,
                link_id = linkId
            }
        );
    }
}
