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
            .AsContractResultAsync(dbModel => dbModel.ToSummaryContract(), 201);
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
            .AsContractResultAsync(dbModel => dbModel.ToSummaryContract());
    }

    public override Task<IActionResult> DeleteBugStepAttachment(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteBugStepAttachmentAsync(user, aliasId, bugId, stepId, id).AsActionResultAsync();
    }

    public override async Task<IActionResult> GetBugStepAttachmentContent(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetBugStepAttachmentContentAsync(user, aliasId, bugId, stepId, id);
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

    public override async Task<IActionResult> GetBugStepAttachmentPreview(
        string aliasId,
        int bugId,
        int stepId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetBugStepAttachmentPreviewContentAsync(user, aliasId, bugId, stepId, id);
        if (attachResult.HasError)
        {
            return attachResult.AsActionResult();
        }

        return new FileStreamResult(attachResult.Value!, AttachmentConstants.PreviewMimeType);
    }
}
