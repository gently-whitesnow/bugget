using Bugget.BO.Errors;
using Bugget.BO.Mappers;
using Bugget.BO.Services.Internal;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.Constants;
using Bugget.Entities.Views.Attachment;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Bugget.Controllers.Internal;

/// <summary>
/// POST /v2/_internal/attachments?bugId=|commentId= — multipart upload attachment'ов
/// от beta-bot'а: либо к Bug'у (создание репорта), либо к Comment'у (диалог тестера).
/// Caller передаёт ровно один из параметров. Auth через `X-Client-Name`
/// (InternalClient scheme). См. TECHSPEC §4.3.2/§4.3.3.
/// </summary>
[Route("/v2/_internal/attachments")]
public sealed class InternalAttachmentsController(InternalAttachmentsService service) : ApiController
{
    private static readonly bool IsDevelopment =
        Environment.GetEnvironmentVariable(EnvironmentConstants.AspnetcoreEnvironment)?
            .Equals("development", StringComparison.OrdinalIgnoreCase) ?? false;

    [HttpPost]
    [ProducesResponseType(typeof(AttachmentView), 201)]
    public async Task<IActionResult> CreateAsync(
        [FromQuery(Name = "bugId")] int? bugId,
        [FromQuery(Name = "commentId")] int? commentId,
        [FromQuery(Name = "attachType")] int? attachType,
        [FromQuery(Name = "stepId")] int? stepId,
        IFormFile file,
        CancellationToken ct)
    {
        if (bugId.HasValue == commentId.HasValue)
        {
            return BadRequest(BoErrors.AttachmentTargetRequired.Error);
        }

        var resolvedAttachType = attachType is null ? AttachType.Fact : (AttachType)attachType.Value;
        if (resolvedAttachType == AttachType.BugStep && (stepId is null || !bugId.HasValue))
        {
            return BadRequest(BoErrors.AttachmentTargetRequired.Error);
        }

        if (stepId.HasValue && resolvedAttachType != AttachType.BugStep)
        {
            return BadRequest(BoErrors.AttachmentTargetRequired.Error);
        }

        Stream fileStream = file.OpenReadStream();
        if (!fileStream.CanSeek)
        {
            fileStream = new FileBufferingReadStream(
                HttpContext.Request.Body,
                1024 * 1024,
                8 * 1024,
                Path.GetTempPath());
            await file.CopyToAsync(fileStream, ct);
            fileStream.Position = 0;
        }

        var mimeType = IsDevelopment ? file.ContentType : await MimeHelper.GuessMimeAsync(fileStream, ct);
        var fileMeta = new FileMeta(file.FileName, file.Length, mimeType);

        if (commentId.HasValue)
        {
            return await service.UploadCommentAttachmentAsync(commentId.Value, fileStream, fileMeta, ct)
                .AsActionResultAsync(AttachmentMapper.ToView, 201);
        }

        if (stepId.HasValue)
        {
            return await service.UploadBugStepAttachmentAsync(bugId!.Value, stepId.Value, fileStream, fileMeta, ct)
                .AsActionResultAsync(AttachmentMapper.ToView, 201);
        }

        return await service.UploadAsync(bugId!.Value, resolvedAttachType, fileStream, fileMeta, ct)
            .AsActionResultAsync(AttachmentMapper.ToView, 201);
    }

    /// <summary>
    /// GET /v2/_internal/attachments/{id}/content — стрим байтов attachment'а для
    /// beta-bot'а (forwarding dev→TG в диалоге). ACL — `X-Client-Name: beta-bot`,
    /// дальнейших проверок нет: caller уже доверенный. См. TECHSPEC §4.3.3.
    /// </summary>
    [HttpGet("{attachmentId:int}/content")]
    public async Task<IActionResult> DownloadAsync([FromRoute] int attachmentId)
    {
        var dbModel = await service.GetAttachmentForDownloadAsync(attachmentId);
        if (dbModel is null)
        {
            return NotFound(BoErrors.AttachmentNotFound.Error);
        }

        var content = await service.OpenContentStreamAsync(dbModel);
        return new FileStreamResult(content, dbModel.MimeType)
        {
            FileDownloadName = dbModel.FileName,
        };
    }
}
