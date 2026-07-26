using System.ComponentModel.DataAnnotations;
using Bugget.BO.Mappers;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.Constants;
using Bugget.Entities.DTO.Attachment;
using Bugget.Entities.Views.Attachment;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Bugget.Controllers;

[Route("/v2/reports/{aliasId}/bugs/{bugId}/attachments")]
public sealed class BugAttachmentsController(AttachmentService attachmentService) : ApiController
{
    private static bool IsDevelopment =
        Environment.GetEnvironmentVariable(EnvironmentConstants.AspnetcoreEnvironment)?
            .Equals("development", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Добавить вложение к багу
    /// </summary>
    /// <param name="aliasId"> alias идентификатора репорта </param>
    /// <param name="bugId"> ID бага </param>
    /// <param name="attachType"> Тип вложения (0 = Fact, 1 = Expected) </param>
    /// <param name="file"> Файл</param>
    /// <param name="ct"> Cancellation token </param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(AttachmentView), 201)]
    public async Task<IActionResult> CreateAttachment(
        [FromRoute] string aliasId,
        [FromRoute] int bugId,
        [Required][FromQuery(Name = "attachType")] int attachType,
        IFormFile file,
        CancellationToken ct
        )
    {
        // Открываем поток. IFormFile.OpenReadStream обычно CanSeek == true,
        // но если вдруг нет, то оборачиваем в FileBufferingReadStream:
        Stream fileStream = file.OpenReadStream();
        if (!fileStream.CanSeek)
        {
            fileStream = new FileBufferingReadStream(
                HttpContext.Request.Body,
                _ = 1024 * 1024,    // threshold before disk
                _ = 8 * 1024,         // buffer size
                _ = Path.GetTempPath()
            );
            // Переписать файл из Request.Body в наш буфер:
            await file.CopyToAsync(fileStream, ct);
            fileStream.Position = 0;
        }

        // Детектим MIME
        var mimeType = IsDevelopment ? file.ContentType : await MimeHelper.GuessMimeAsync(fileStream, ct);

        return await attachmentService.SaveBugAttachmentAsync(
            User.GetIdentity(),
            aliasId,
            bugId,
            fileStream,
            (AttachType)attachType,
            new FileMeta(file.FileName, file.Length, mimeType),
            ct)
            .AsActionResultAsync((attachmentDbModel) => AttachmentMapper.ToView(attachmentDbModel), 201);
    }

    /// <summary>
    /// Получить содержимое вложения
    /// </summary>
    /// <param name="aliasId"> alias идентификатора репорта </param>
    /// <param name="bugId"> ID бага </param>
    /// <param name="id"> ID вложения </param>
    /// <returns></returns>
    [HttpGet("{id}/content")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> GetAttachmentContentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int id)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetBugAttachmentContentAsync(user, aliasId, bugId, id);
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

    /// <summary>
    /// Получить превью содержимого вложения
    /// </summary>
    /// <param name="aliasId"> alias идентификатора репорта </param>
    /// <param name="bugId"> ID бага </param>
    /// <param name="id"> ID вложения </param>
    /// <returns></returns>
    [HttpGet("{id}/content/preview")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> GetAttachmentPreviewContentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int id)
    {
        var user = User.GetIdentity();
        var attachResult = await attachmentService.GetBugAttachmentPreviewContentAsync(user, aliasId, bugId, id);
        if (attachResult.HasError)
        {
            return attachResult.AsActionResult();
        }

        return new FileStreamResult(attachResult.Value!, AttachmentConstants.PreviewMimeType);
    }

    /// <summary>
    /// Удалить вложение
    /// </summary>
    /// <param name="aliasId"> alias идентификатора репорта </param>
    /// <param name="bugId"> ID бага </param>
    /// <param name="id"> ID вложения </param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteAttachmentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int id)
    {
        var user = User.GetIdentity();
        return attachmentService.DeleteBugAttachmentAsync(user, aliasId, bugId, id).AsActionResultAsync();
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(AttachmentView), 200)]
    public Task<IActionResult> RenameAttachmentAsync(
        [FromRoute] string aliasId,
        [FromRoute] int bugId,
        [FromRoute] int id,
        [FromBody] RenameAttachmentDto dto)
    {
        var user = User.GetIdentity();
        return attachmentService.RenameBugAttachmentAsync(user, aliasId, bugId, id, dto.FileName)
            .AsActionResultAsync(AttachmentMapper.ToView);
    }
}
