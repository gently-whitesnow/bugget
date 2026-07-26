using Bugget.BO.Mappers;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.Constants;
using Bugget.Entities.DTO.Attachment;
using Bugget.Entities.Views.Attachment;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Bugget.Controllers.Bugs;

[Route("/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments")]
public sealed class BugStepAttachmentsController(AttachmentService attachmentService) : ApiController
{
    private static bool IsDevelopment =
        Environment.GetEnvironmentVariable(EnvironmentConstants.AspnetcoreEnvironment)?
            .Equals("development", StringComparison.OrdinalIgnoreCase) ?? false;

    [HttpPost]
    [ProducesResponseType(typeof(AttachmentView), 201)]
    public async Task<IActionResult> CreateAttachment(
        [FromRoute] string aliasId,
        [FromRoute] int bugId,
        [FromRoute] int stepId,
        IFormFile file,
        CancellationToken ct)
    {
        Stream fileStream = file.OpenReadStream();
        if (!fileStream.CanSeek)
        {
            fileStream = new FileBufferingReadStream(
                HttpContext.Request.Body,
                _ = 1024 * 1024,
                _ = 8 * 1024,
                _ = Path.GetTempPath()
            );

            await file.CopyToAsync(fileStream, ct);
            fileStream.Position = 0;
        }

        var mimeType = IsDevelopment ? file.ContentType : await MimeHelper.GuessMimeAsync(fileStream, ct);

        return await attachmentService.SaveBugStepAttachmentAsync(
                User.GetIdentity(),
                aliasId,
                bugId,
                stepId,
                fileStream,
                new FileMeta(file.FileName, file.Length, mimeType),
                ct)
            .AsActionResultAsync(AttachmentMapper.ToView, 201);
    }

    [HttpGet("{id}/content")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> GetAttachmentContentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int stepId, [FromRoute] int id)
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

    [HttpGet("{id}/content/preview")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> GetAttachmentPreviewContentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int stepId, [FromRoute] int id)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetBugStepAttachmentPreviewContentAsync(user, aliasId, bugId, stepId, id);
        if (attachResult.HasError)
        {
            return attachResult.AsActionResult();
        }

        return new FileStreamResult(attachResult.Value!, AttachmentConstants.PreviewMimeType);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteAttachmentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int stepId, [FromRoute] int id)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteBugStepAttachmentAsync(user, aliasId, bugId, stepId, id).AsActionResultAsync();
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(AttachmentView), 200)]
    public Task<IActionResult> RenameAttachmentAsync(
        [FromRoute] string aliasId,
        [FromRoute] int bugId,
        [FromRoute] int stepId,
        [FromRoute] int id,
        [FromBody] RenameAttachmentDto dto)
    {
        var user = User.GetIdentity();
        return attachmentService.RenameBugStepAttachmentAsync(user, aliasId, bugId, stepId, id, dto.FileName)
            .AsActionResultAsync(AttachmentMapper.ToView);
    }
}
