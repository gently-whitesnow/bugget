using Bugget.Api.Extensions;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;
using Bugget.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Controllers.Attachments;

/// <summary>Отдача содержимого и превью — одна на контроллеры вложений бага, шага и комментария.</summary>
internal static class AttachmentFileResults
{
    public static async Task<IActionResult> AsAttachmentContentAsync(
        this Task<((Stream Content, Attachment Model)? Value, Error? Error)> request,
        HttpContext context)
    {
        var (attachment, error) = await request;
        if (error is not null)
        {
            return error.ToProblemDetails(context);
        }

        var (content, meta) = attachment!.Value;
        if (meta.IsGzipCompressed == true)
        {
            context.Response.Headers.ContentEncoding = "gzip";
        }

        return new FileStreamResult(content, meta.MimeType);
    }

    public static async Task<IActionResult> AsAttachmentPreviewAsync(
        this Task<(Stream? Value, Error? Error)> request,
        HttpContext context)
    {
        var (content, error) = await request;
        return error is not null
            ? error.ToProblemDetails(context)
            : new FileStreamResult(content!, AttachmentConstants.PreviewMimeType);
    }
}
