using System.ComponentModel.DataAnnotations;
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
/// Вложения бага. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="BugAttachmentsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class BugAttachmentsController(
    IAttachmentService attachmentService,
    IMimeTypeDetector mimeTypeDetector) : BugAttachmentsControllerBase
{
    public override async Task<ActionResult<AttachmentSummary>> CreateBugAttachment(
        string aliasId,
        int bugId,
        // Обязательность query-параметра генератор в атрибут не переносит, а без неё
        // пропущенный attachType связался бы первым значением enum'а и вложение
        // легло бы «в факт».
        [Required] AttachType attachType,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var (content, meta) = await AttachmentUploadReader.ReadAsync(file, mimeTypeDetector, cancellationToken);

        return await attachmentService.SaveBugAttachmentAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            content,
            attachType.ToDomain(),
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

    public override Task<IActionResult> GetBugAttachmentContent(
        string aliasId,
        int bugId,
        int id,
        CancellationToken cancellationToken = default) =>
        attachmentService.GetBugAttachmentContentAsync(User.GetIdentity(), aliasId, bugId, id)
            .AsAttachmentContentAsync(HttpContext);

    public override Task<IActionResult> GetBugAttachmentPreview(
        string aliasId,
        int bugId,
        int id,
        CancellationToken cancellationToken = default) =>
        attachmentService.GetBugAttachmentPreviewContentAsync(User.GetIdentity(), aliasId, bugId, id)
            .AsAttachmentPreviewAsync(HttpContext);
}
