using System.Text.Json;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DbModels.Comment;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class CommentsDbClient : PostgresClient, ICommentsDbClient
{
    public async Task<CommentSummaryDbModel> CreateCommentAsync(
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<CommentSummaryDbModel>(
            "SELECT * FROM public.create_comment_internal(@user_id, @bug_id, @text, @creator_type, @audience);",
            new
            {
                user_id = userId,
                bug_id = bugId,
                text = text,
                creator_type = (short)creatorType,
                audience = (short)audience
            }
        );
    }

    public Task<CommentSummaryDbModel> CreateCommentAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<CommentSummaryDbModel>(new CommandDefinition(
            "SELECT * FROM public.create_comment_internal(@user_id, @bug_id, @text, @creator_type, @audience);",
            new
            {
                user_id = userId,
                bug_id = bugId,
                text = text,
                creator_type = (short)creatorType,
                audience = (short)audience
            },
            transaction: tx));
    }

    /// <summary>
    /// Чтение external-комментариев для _internal contract (TECHSPEC §4.3.4).
    /// Жёсткий фильтр <c>audience = External</c> в SQL — I-1 инвариант: internal-комментарии
    /// никогда не покидают query path, даже при баге на уровне caller'а.
    /// </summary>
    public async Task<IReadOnlyList<CommentSummaryDbModel>> ListExternalCommentsByBugAsync(
        int bugId,
        int sinceId,
        int limit)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CommentSummaryDbModel>(
            @"SELECT c.id, c.bug_id, c.text, c.creator_user_id, c.creator_type,
                     c.audience, c.created_at, c.updated_at
              FROM public.comments c
              WHERE c.bug_id = @bug_id AND c.audience = @audience AND c.id > @since_id
              ORDER BY c.id
              LIMIT @limit;",
            new
            {
                bug_id = bugId,
                audience = (short)CommentAudience.External,
                since_id = sinceId,
                limit = limit,
            });

        return rows.AsList();
    }

    /// <summary>
    /// Резолв report+workspace+alias-координат комментария по его id (без bugId/reportId на входе).
    /// Используется `_internal` контрактом загрузки comment-attachment'а от beta-bot,
    /// где caller имеет только `commentId` после успешного `POST /v2/_internal/bugs/{bugId}/comments`.
    /// </summary>
    public async Task<CommentLocatorDbModel?> GetCommentLocatorAsync(int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentLocatorDbModel>(
            @"SELECT c.id AS CommentId,
                     c.bug_id AS BugId,
                     b.report_id AS ReportId,
                     c.creator_user_id AS CreatorUserId,
                     r.creator_team_id AS CreatorTeamId,
                     r.creator_organization_id AS CreatorOrganizationId,
                     r.public_id AS PublicId,
                     r.team_report_id AS TeamReportId
              FROM public.comments c
              JOIN public.bugs b ON b.id = c.bug_id
              JOIN public.reports r ON r.id = b.report_id
              WHERE c.id = @comment_id
              LIMIT 1;",
            new { comment_id = commentId });
    }

    public async Task<CommentSummaryDbModel?> GetCommentAsync(int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummaryDbModel>(
            @"SELECT c.id, c.bug_id, c.text, c.creator_user_id, c.creator_type,
                     c.audience, c.created_at, c.updated_at
              FROM public.comments c
              WHERE c.id = @comment_id
              LIMIT 1;",
            new { comment_id = commentId });
    }

    public async Task<CommentSummaryDbModel?> DeleteCommentInternalAsync(string userId, int reportId, int bugId, int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummaryDbModel>(
            "SELECT public.delete_comment_internal(@user_id, @report_id, @bug_id, @comment_id);",
            new
            {
                user_id = userId,
                report_id = reportId,
                bug_id = bugId,
                comment_id = commentId
            }
        );
    }

    public async Task<CommentSummaryDbModel?> UpdateCommentInternalAsync(string userId, int reportId, int bugId, int commentId, string text)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummaryDbModel>(
            "SELECT * FROM public.update_comment_internal(@user_id, @report_id, @bug_id, @comment_id, @text);",
            new
            {
                user_id = userId,
                report_id = reportId,
                bug_id = bugId,
                comment_id = commentId,
                text = text
            }
        );
    }
}
