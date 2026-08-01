using Bugget.Application.Commands.Comment;
using Bugget.Domain.Authentication;
using Bugget.Domain.Comments;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.Comments;

public interface ICommentsService
{
    Task<(CommentSummary? Value, Error? Error)> CreateCommentAsync(UserIdentity user, string aliasId, int bugId, CommentDto commentDto);
    Task<Error?> DeleteCommentAsync(UserIdentity user, string aliasId, int bugId, int commentId);
    Task<(CommentSummary? Value, Error? Error)> UpdateCommentAsync(UserIdentity user, string aliasId, int bugId, int commmentId, CommentDto commentDto);
}
