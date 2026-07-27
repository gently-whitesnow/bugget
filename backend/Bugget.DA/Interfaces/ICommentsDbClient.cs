using Bugget.DA.Transactions;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DbModels.Comment;

namespace Bugget.DA.Interfaces;

public interface ICommentsDbClient
{
    Task<CommentSummaryDbModel> CreateCommentAsync(
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal);

    Task<CommentSummaryDbModel> CreateCommentAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User,
        int audience = (int)CommentAudience.Internal);



    Task<CommentSummaryDbModel?> GetCommentAsync(int commentId);

    Task<CommentSummaryDbModel?> DeleteCommentInternalAsync(string userId, int reportId, int bugId, int commentId);

    Task<CommentSummaryDbModel?> UpdateCommentInternalAsync(string userId, int reportId, int bugId, int commentId, string text);
}
