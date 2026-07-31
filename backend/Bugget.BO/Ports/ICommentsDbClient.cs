using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.Common;

namespace Bugget.BO.Ports;

public interface ICommentsDbClient
{
    Task<CommentSummary> CreateCommentAsync(
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal);

    Task<CommentSummary> CreateCommentAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal);



    Task<CommentSummary?> GetCommentAsync(int commentId);

    Task<CommentSummary?> DeleteCommentInternalAsync(string userId, int reportId, int bugId, int commentId);

    Task<CommentSummary?> UpdateCommentInternalAsync(string userId, int reportId, int bugId, int commentId, string text);
}
