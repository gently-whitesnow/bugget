using System.ComponentModel.DataAnnotations;
using Bugget.Api.Generated.Reports;
using Bugget.BO.Services.Attachments;
using Bugget.Controllers.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO;
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
/// Вложения бага. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="BugAttachmentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class BugAttachmentsController(AttachmentService attachmentService) : BugAttachmentsControllerBase
{
    public override async Task<ActionResult<AttachmentSummary>> CreateBugAttachment(
        string aliasId,
        int bugId,
        // Обязательность query-параметра генератор в атрибут не переносит, а без неё
        // пропущенный attachType связался бы нулём и вложение легло бы «в факт».
        [Required] int attachType,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var (content, meta) = await AttachmentUploadReader.ReadAsync(file, cancellationToken);

        return await attachmentService.SaveBugAttachmentAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            content,
            (AttachType)attachType,
            meta,
            cancellationToken)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<AttachmentSummary>> RenameBugAttachment(
        string aliasId,
        int bugId,
        int id,
        AttachmentRenameRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.RenameBugAttachmentAsync(user, aliasId, bugId, id, body.File_name)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract());
    }

    public override Task<IActionResult> DeleteBugAttachment(
        string aliasId,
        int bugId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteBugAttachmentAsync(user, aliasId, bugId, id).AsActionResultAsync(HttpContext);
    }

    public override async Task<IActionResult> GetBugAttachmentContent(
        string aliasId,
        int bugId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var (attachment, error) = await attachmentService.GetBugAttachmentContentAsync(user, aliasId, bugId, id);
        if (error is not null)
        {
            return error.ToProblemDetails(HttpContext);
        }

        var (content, meta) = attachment!.Value;
        if (meta.IsGzipCompressed == true)
        {
            Response.Headers["Content-Encoding"] = "gzip";
        }

        return new FileStreamResult(content, meta.MimeType);
    }

    public override async Task<IActionResult> GetBugAttachmentPreview(
        string aliasId,
        int bugId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var (content, error) = await attachmentService.GetBugAttachmentPreviewContentAsync(user, aliasId, bugId, id);
        if (error is not null)
        {
            return error.ToProblemDetails(HttpContext);
        }

        return new FileStreamResult(content!, AttachmentConstants.PreviewMimeType);
    }
}
