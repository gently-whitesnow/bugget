using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Services.Comments;
using Bugget.Contracts.Dto.Comment;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Controllers.Comments;

/// <summary>
/// Api для работы с комментами. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="CommentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class CommentsController(CommentsService commentsService) : CommentsControllerBase
{
    public override Task<ActionResult<CommentSummary>> CreateComment(
        string aliasId,
        int bugId,
        CommentRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return commentsService.CreateCommentAsync(user, aliasId, bugId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<CommentSummary>> UpdateComment(
        string aliasId,
        int bugId,
        int commentId,
        CommentRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return commentsService.UpdateCommentAsync(user, aliasId, bugId, commentId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract());
    }

    public override Task<IActionResult> DeleteComment(
        string aliasId,
        int bugId,
        int commentId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return commentsService.DeleteCommentAsync(user, aliasId, bugId, commentId).AsActionResultAsync(HttpContext);
    }

    private static CommentDto ToDto(CommentRequest body) => new()
    {
        Text = body.Text,
        Audience = (short?)body.Audience
    };
}
