using System.ComponentModel.DataAnnotations;
using Bugget.Api.Controllers.Attachments;
using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Commands.Comment;
using Bugget.Application.Ports;
using Bugget.Application.Services.Comments;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
using FileParameter = Bugget.Api.Generated.Reports.FileParameter;

namespace Bugget.Api.Controllers.Comments;

/// <summary>
/// Api для работы с комментами. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="CommentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class CommentsController(
    ICommentsService commentsService,
    IMimeTypeDetector mimeTypeDetector) : CommentsControllerBase
{
    public override Task<ActionResult<CommentSummary>> CreateComment(
        string aliasId,
        int bugId,
        CommentRequest body,
        CancellationToken cancellationToken = default) =>
        commentsService.CreateCommentAsync(User.GetIdentity(), aliasId, bugId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);

    public override async Task<ActionResult<Comment>> CreateCommentWithAttachments(
        string aliasId,
        int bugId,
        [FromForm, Required, StringLength(2048, MinimumLength = 1)] string text,
        [FromForm] CommentAudience? audience,
        [FromForm] IEnumerable<FileParameter> files,
        CancellationToken cancellationToken = default)
    {
        var uploads = await AttachmentUploadReader.ReadManyAsync(files, mimeTypeDetector, cancellationToken);
        var dto = new CommentDto { Text = text, Audience = (short?)audience?.ToDomainValue() };

        return await commentsService.CreateCommentWithAttachmentsAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            dto,
            uploads,
            cancellationToken)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract(), 201);
    }

    public override Task<ActionResult<CommentSummary>> UpdateComment(
        string aliasId,
        int bugId,
        int commentId,
        CommentRequest body,
        CancellationToken cancellationToken = default) =>
        commentsService.UpdateCommentAsync(User.GetIdentity(), aliasId, bugId, commentId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract());

    public override Task<IActionResult> DeleteComment(
        string aliasId,
        int bugId,
        int commentId,
        CancellationToken cancellationToken = default) =>
        commentsService.DeleteCommentAsync(User.GetIdentity(), aliasId, bugId, commentId).AsActionResultAsync(HttpContext);

    private static CommentDto ToDto(CommentRequest body) => new()
    {
        Text = body.Text,
        Audience = (short?)body.Audience?.ToDomainValue()
    };
}
