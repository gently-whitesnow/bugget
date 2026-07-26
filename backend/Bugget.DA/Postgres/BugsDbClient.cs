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

    public Task<BugSummaryDbModel> CreateBugAsync(
        ITransactionScope scope,
        string userId,
        int reportId,
        BugDto bugDto,
        short creatorType)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugSummaryDbModel>(new CommandDefinition(
            "SELECT * FROM public.create_bug_internal(@user_id, @report_id, @receive, @expect, @title, @creator_type);",
            new
            {
                user_id = userId,
                report_id = reportId,
                receive = bugDto.Receive,
                expect = bugDto.Expect,
                title = bugDto.Title,
                creator_type = creatorType,
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

    /// <summary>
    /// Резолв report+workspace-координат Bug'а по его id (без обязательного
    /// reportId на входе). Нужен internal-контрактам вида `?bugId=` (TECHSPEC §4.3.2),
    /// где caller имеет только bugId после успешного `POST /v2/_internal/bugs`.
    /// </summary>
    public async Task<BugLocatorDbModel?> GetBugLocatorAsync(int bugId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<BugLocatorDbModel>(
            @"SELECT b.report_id AS ReportId,
                     b.creator_user_id AS CreatorUserId,
                     r.creator_team_id AS CreatorTeamId,
                     r.creator_organization_id AS CreatorOrganizationId,
                     r.public_id AS PublicId,
                     r.team_report_id AS TeamReportId
              FROM public.bugs b
              JOIN public.reports r ON r.id = b.report_id
              WHERE b.id = @bug_id
              LIMIT 1;",
            new { bug_id = bugId });
    }

    /// <summary>
    /// Полная карточка bug'а (bug+report+attachments_count) для рендера в Telegram-боте.
    /// TECHSPEC §4.3.6, BETA-BOT-UX-CARD-FULL-DATA.
    /// </summary>
    public async Task<BugDetailDbModel?> GetBugDetailInternalAsync(
        int bugId,
        int[] bugAttachTypes,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                b.id              AS bug_id,
                r.id              AS report_id,
                r.team_report_id  AS report_number,
                r.status          AS report_status,
                b.title           AS title,
                b.status          AS status,
                b.creator_type    AS creator_type,
                b.creator_user_id AS creator_user_id,
                b.receive         AS receive,
                b.expect          AS expect,
                b.created_at      AS created_at,
                b.updated_at      AS updated_at,
                COALESCE((
                    SELECT count(*)::int
                    FROM public.attachments a
                    WHERE a.entity_id = b.id
                      AND a.attach_type = ANY(@bugAttachTypes)
                ), 0)             AS attachments_count
            FROM public.bugs b
            JOIN public.reports r ON r.id = b.report_id
            WHERE b.id = @bugId
            LIMIT 1;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<BugDetailDbModel>(new CommandDefinition(
            sql,
            new { bugId, bugAttachTypes },
            cancellationToken: ct));
    }
}
