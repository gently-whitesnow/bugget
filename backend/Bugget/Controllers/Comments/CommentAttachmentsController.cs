using Bugget.Api.Generated.Reports;
using Bugget.BO.Services.Attachments;
using Bugget.Controllers.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.Constants;
using Bugget.Extensions;
using Bugget.Mappers;
using Bugget.Reports.Contracts.Generated;
using Microsoft.AspNetCore.Mvc;
// NSwag эмитит FileParameter и в файл контроллеров, и в файл DTO — берём тот,
// что стоит в сигнатуре сгенерированной базы.
using FileParameter = Bugget.Api.Generated.Reports.FileParameter;

namespace Bugget.Controllers;

/// <summary>
/// Вложения комментария. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через
/// <see cref="CommentAttachmentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class CommentAttachmentsController(AttachmentService attachmentService) : CommentAttachmentsControllerBase
{
    public override async Task<ActionResult<AttachmentSummary>> CreateCommentAttachment(
        string aliasId,
        int bugId,
        int commentId,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var (content, meta) = await AttachmentUploadReader.ReadAsync(file, cancellationToken);

        return await attachmentService.SaveCommentAttachmentAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            commentId,
            content,
            meta,
            cancellationToken)
            .AsContractResultAsync(dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<AttachmentSummary>> RenameCommentAttachment(
        string aliasId,
        int bugId,
        int commentId,
        int id,
        AttachmentRenameRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.RenameCommentAttachmentAsync(user, aliasId, bugId, commentId, id, body.File_name)
            .AsContractResultAsync(dbModel => dbModel.ToSummaryContract());
    }

    public override Task<IActionResult> DeleteCommentAttachment(
        string aliasId,
        int bugId,
        int commentId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteCommentAttachmentAsync(user, aliasId, bugId, commentId, id).AsActionResultAsync();
    }

    public override async Task<IActionResult> GetCommentAttachmentContent(
        string aliasId,
        int bugId,
        int commentId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetCommentAttachmentContentAsync(user, aliasId, bugId, commentId, id);
        if (attachResult.HasError)
        {
            return attachResult.AsActionResult();
        }

        var (content, attachmentDbModel) = attachResult.Value;
        if (attachmentDbModel.IsGzipCompressed == true)
        {
            Response.Headers["Content-Encoding"] = "gzip";
        }

        return new FileStreamResult(content, attachmentDbModel.MimeType);
    }

    public override async Task<IActionResult> GetCommentAttachmentPreview(
        string aliasId,
        int bugId,
        int commentId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetCommentAttachmentPreviewContentAsync(user, aliasId, bugId, commentId, id);
        if (attachResult.HasError)
        {
            return attachResult.AsActionResult();
        }

        return new FileStreamResult(attachResult.Value!, AttachmentConstants.PreviewMimeType);
    }
}
