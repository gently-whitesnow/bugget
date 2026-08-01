using System.Text.Json;
using Bugget.Application.Ports;
using Bugget.Domain.Comments;
using Bugget.Domain.Common;
using Bugget.Infrastructure.Transactions;
using Dapper;

namespace Bugget.Infrastructure.Postgres;

public sealed class CommentsDbClient : PostgresClient, ICommentsDbClient
{
    public async Task<CommentSummary> CreateCommentAsync(
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<CommentSummary>(
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

    public Task<CommentSummary> CreateCommentAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<CommentSummary>(new CommandDefinition(
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
    public async Task<CommentSummary?> GetCommentAsync(int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummary>(
            @"SELECT c.id, c.bug_id, c.text, c.creator_user_id, c.creator_type,
                     c.audience, c.created_at, c.updated_at
              FROM public.comments c
              WHERE c.id = @comment_id
              LIMIT 1;",
            new { comment_id = commentId });
    }

    public async Task<CommentSummary?> DeleteCommentInternalAsync(string userId, int reportId, int bugId, int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummary>(
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

    public async Task<CommentSummary?> UpdateCommentInternalAsync(string userId, int reportId, int bugId, int commentId, string text)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<CommentSummary>(
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
