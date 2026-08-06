using Bugget.Application.Commands.Bug;
using Bugget.Application.Ports;
using Bugget.Domain.Bugs;
using Bugget.Domain.Common;
using Bugget.Infrastructure.Transactions;
using Dapper;

namespace Bugget.Infrastructure.Postgres;

public sealed class BugsDbClient : PostgresClient, IBugsDbClient
{
    public async Task<BugSummary> CreateBugAsync(
        string userId,
        int reportId,
        BugDto bugDto,
        int creatorType = (int)CreatorType.User)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugSummary>(
            "SELECT * FROM public.create_bug_internal(@user_id, @report_id, @receive, @expect, @title, @creator_type);",
            new
            {
                user_id = userId,
                report_id = reportId,
                receive = bugDto.Receive,
                expect = bugDto.Expect,
                title = bugDto.Title,
                creator_type = (short)creatorType,
            }
                );
    }

    public Task<BugSummary> CreateBugAsync(
        ITransactionScope scope,
        string userId,
        int reportId,
        BugDto bugDto,
        int creatorType = (int)CreatorType.User)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugSummary>(new CommandDefinition(
            "SELECT * FROM public.create_bug_internal(@user_id, @report_id, @receive, @expect, @title, @creator_type);",
            new
            {
                user_id = userId,
                report_id = reportId,
                receive = bugDto.Receive,
                expect = bugDto.Expect,
                title = bugDto.Title,
                creator_type = (short)creatorType,
            },
            transaction: tx));
    }

    public async Task<BugPatchResult> PatchBugAsync(int reportId, int bugId, BugPatchDto patchDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugPatchResult>(
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

    public Task<BugPatchResult> PatchBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId,
        BugPatchDto patchDto)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugPatchResult>(new CommandDefinition(
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

    public async Task<BugSummary?> GetBugAsync(int reportId, int bugId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<BugSummary>(
            "SELECT * FROM public.get_bug_internal(@report_id, @bug_id);",
            new { report_id = reportId, bug_id = bugId }
        );
    }

    public Task<BugSummary?> GetBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleOrDefaultAsync<BugSummary>(new CommandDefinition(
            "SELECT * FROM public.get_bug_internal(@report_id, @bug_id);",
            new { report_id = reportId, bug_id = bugId },
            transaction: tx));
    }
}
