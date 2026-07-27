using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DTO.Bug;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class BugsDbClient : PostgresClient, IBugsDbClient
{
    public async Task<BugSummaryDbModel> CreateBugAsync(string userId, int reportId, BugDto bugDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugSummaryDbModel>(
            "SELECT * FROM public.create_bug_internal(@user_id, @report_id, @receive, @expect, @title);",
            new
            {
                user_id = userId,
                report_id = reportId,
                receive = bugDto.Receive,
                expect = bugDto.Expect,
                title = bugDto.Title,
            }
                );
    }

    public Task<BugSummaryDbModel> CreateBugAsync(
        ITransactionScope scope,
        string userId,
        int reportId,
        BugDto bugDto)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugSummaryDbModel>(new CommandDefinition(
            "SELECT * FROM public.create_bug_internal(@user_id, @report_id, @receive, @expect, @title);",
            new
            {
                user_id = userId,
                report_id = reportId,
                receive = bugDto.Receive,
                expect = bugDto.Expect,
                title = bugDto.Title,
            },
            transaction: tx));
    }

    public async Task<BugPatchResultDbModel> PatchBugAsync(int reportId, int bugId, BugPatchDto patchDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugPatchResultDbModel>(
            "SELECT * FROM public.patch_bug_internal(@bug_id, @report_id, @receive, @expect, @status, @title);",
            new
            {
                bug_id = bugId,
                report_id = reportId,
                receive = patchDto.Receive,
                expect = patchDto.Expect,
                status = patchDto.Status,
                title = patchDto.Title,
            }
        );
    }

    public Task<BugPatchResultDbModel> PatchBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId,
        BugPatchDto patchDto)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugPatchResultDbModel>(new CommandDefinition(
            "SELECT * FROM public.patch_bug_internal(@bug_id, @report_id, @receive, @expect, @status, @title);",
            new
            {
                bug_id = bugId,
                report_id = reportId,
                receive = patchDto.Receive,
                expect = patchDto.Expect,
                status = patchDto.Status,
                title = patchDto.Title,
            },
            transaction: tx));
    }

    public async Task<BugSummaryDbModel?> GetBugAsync(int reportId, int bugId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<BugSummaryDbModel>(
            "SELECT * FROM public.get_bug_internal(@report_id, @bug_id);",
            new { report_id = reportId, bug_id = bugId }
        );
    }

    public Task<BugSummaryDbModel?> GetBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleOrDefaultAsync<BugSummaryDbModel>(new CommandDefinition(
            "SELECT * FROM public.get_bug_internal(@report_id, @bug_id);",
            new { report_id = reportId, bug_id = bugId },
            transaction: tx));
    }
}
