namespace Bugget.Entities.Constants;

public static class AttachmentConstants
{
    public const string PreviewMimeType = "image/webp";

    public static readonly string[] CompressibleMimeTypes =
        { "text/plain", "text/csv", "application/json", "text/markdown", "text/json" };

    public static readonly string[] ImageMimeTypes =
        { "image/png", "image/jpeg", "image/webp" };

    public static readonly string[] MediaMimeTypes =
        { "image/gif" };

    public static readonly string[] VideoMimeTypes =
        { "video/mp4", "video/webm", "video/quicktime", "video/ogg" };

    // Белый список “разрешённых” MIME
    public static readonly Lazy<string[]> AllowedMimes = new Lazy<string[]>(() =>
        ImageMimeTypes
            .Concat(VideoMimeTypes)
            .Concat(MediaMimeTypes)
            .Concat(CompressibleMimeTypes)
            .ToArray());
}
