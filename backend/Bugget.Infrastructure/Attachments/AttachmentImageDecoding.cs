using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Bugget.Infrastructure.Attachments;

public static class AttachmentImageDecoding
{
    // ImageSharp выбирает декодер по содержимому, а не по MIME: TIFF или BMP под видом PNG
    // дошли бы до их декодеров. Оставлены только форматы из AttachmentConstants.ImageMimeTypes.
    public static readonly DecoderOptions Options = new()
    {
        Configuration = new Configuration(
            new PngConfigurationModule(),
            new JpegConfigurationModule(),
            new WebpConfigurationModule())
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            PreferContiguousImageBuffers = true,
        },
    };
}
