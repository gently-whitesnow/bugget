using Bugget.Api.Controllers.Attachments;
using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Services.Attachments;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Bugget.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
// NSwag эмитит FileParameter и в файл контроллеров, и в файл DTO — берём тот,
// что стоит в сигнатуре сгенерированной базы.
using FileParameter = Bugget.Api.Generated.Reports.FileParameter;

namespace Bugget.Api.Controllers;

/// <summary>
/// Вложения шага воспроизведения. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через
/// <see cref="BugStepAttachmentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class BugStepAttachmentsController(AttachmentService attachmentService) : BugStepAttachmentsControllerBase
{
    public override async Task<ActionResult<AttachmentSummary>> CreateBugStepAttachment(
        string aliasId,
        int bugId,
        int stepId,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var (content, meta) = await AttachmentUploadReader.ReadAsync(file, cancellationToken);

        return await attachmentService.SaveBugStepAttachmentAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            stepId,
            content,
            meta,
            cancellationToken)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<AttachmentSummary>> RenameBugStepAttachment(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        AttachmentRenameRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.RenameBugStepAttachmentAsync(user, aliasId, bugId, stepId, id, body.File_name)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract());
    }

    public override Task<IActionResult> DeleteBugStepAttachment(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteBugStepAttachmentAsync(user, aliasId, bugId, stepId, id).AsActionResultAsync(HttpContext);
    }

    public override async Task<IActionResult> GetBugStepAttachmentContent(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var (attachment, error) = await attachmentService.GetBugStepAttachmentContentAsync(user, aliasId, bugId, stepId, id);
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

    public override async Task<IActionResult> GetBugStepAttachmentPreview(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var (content, error) = await attachmentService.GetBugStepAttachmentPreviewContentAsync(user, aliasId, bugId, stepId, id);
        if (error is not null)
        {
            return error.ToProblemDetails(HttpContext);
        }

        return new FileStreamResult(content!, AttachmentConstants.PreviewMimeType);
    }
}
