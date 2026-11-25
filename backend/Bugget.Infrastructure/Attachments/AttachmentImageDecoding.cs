using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Bugget.Infrastructure.Attachments;

public static class AttachmentImageDecoding
{
    // Второй рубеж после белого списка MIME: libmagic и ImageSharp распознают формат каждый по-своему,
    // и собранный файл, который libmagic считает PNG, без этого дошёл бы до декодера TIFF.
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
