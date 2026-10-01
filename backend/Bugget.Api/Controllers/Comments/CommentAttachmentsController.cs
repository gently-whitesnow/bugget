using Bugget.Api.Controllers.Attachments;
using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
// NSwag эмитит FileParameter и в файл контроллеров, и в файл DTO — берём тот,
// что стоит в сигнатуре сгенерированной базы.
using FileParameter = Bugget.Api.Generated.Reports.FileParameter;

namespace Bugget.Api.Controllers;

/// <summary>
/// Вложения комментария. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через
/// <see cref="CommentAttachmentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class CommentAttachmentsController(
    IAttachmentService attachmentService,
    IMimeTypeDetector mimeTypeDetector) : CommentAttachmentsControllerBase
{
    public override Task<IActionResult> GetCommentAttachmentContent(
        string aliasId, int bugId, int commentId, int id, CancellationToken cancellationToken = default) =>
        attachmentService.GetCommentAttachmentContentAsync(User.GetIdentity(), aliasId, bugId, commentId, id)
            .AsAttachmentContentAsync(HttpContext);

    public override Task<IActionResult> GetCommentAttachmentPreview(
        string aliasId, int bugId, int commentId, int id, CancellationToken cancellationToken = default) =>
        attachmentService.GetCommentAttachmentPreviewContentAsync(User.GetIdentity(), aliasId, bugId, commentId, id)
            .AsAttachmentPreviewAsync(HttpContext);

    public override async Task<ActionResult<AttachmentSummary>> CreateCommentAttachment(
        string aliasId, int bugId, int commentId, [FromForm] FileParameter file, CancellationToken cancellationToken = default)
    {
        var (content, meta) = await AttachmentUploadReader.ReadAsync(file, mimeTypeDetector, cancellationToken);

        return await attachmentService
            .SaveCommentAttachmentAsync(User.GetIdentity(), aliasId, bugId, commentId, content, meta, cancellationToken)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<AttachmentSummary>> RenameCommentAttachment(
        string aliasId, int bugId, int commentId, int id, AttachmentRenameRequest body, CancellationToken cancellationToken = default) =>
        attachmentService.RenameCommentAttachmentAsync(User.GetIdentity(), aliasId, bugId, commentId, id, body.File_name)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract());

    public override Task<IActionResult> DeleteCommentAttachment(
        string aliasId, int bugId, int commentId, int id, CancellationToken cancellationToken = default) =>
        attachmentService.DeleteCommentAttachmentAsync(User.GetIdentity(), aliasId, bugId, commentId, id)
            .AsActionResultAsync(HttpContext);
}
