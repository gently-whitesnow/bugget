using Bugget.Infrastructure.Attachments;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bugget.UnitTests.Services.Attachments;

public sealed class AttachmentImageDecodingTests
{
    public static TheoryData<string> AllowedFormats => new() { "png", "jpeg", "webp" };

    public static TheoryData<string> RejectedFormats => new() { "tiff", "bmp" };

    [Theory]
    [MemberData(nameof(AllowedFormats))]
    public async Task Decodes_formats_allowed_for_attachments(string format)
    {
        using var stream = Encode(format);

        using var image = await Image.LoadAsync<Rgba32>(AttachmentImageDecoding.Options, stream);

        Assert.Equal(4, image.Width);
    }

    [Theory]
    [MemberData(nameof(RejectedFormats))]
    public async Task Rejects_formats_outside_attachment_whitelist(string format)
    {
        using var stream = Encode(format);

        await Assert.ThrowsAsync<UnknownImageFormatException>(
            () => Image.LoadAsync<Rgba32>(AttachmentImageDecoding.Options, stream));
    }

    private static MemoryStream Encode(string format)
    {
        IImageEncoder encoder = format switch
        {
            "png" => new PngEncoder(),
            "jpeg" => new JpegEncoder(),
            "webp" => new WebpEncoder(),
            "tiff" => new TiffEncoder(),
            "bmp" => new BmpEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        using var image = new Image<Rgba32>(4, 4);
        var stream = new MemoryStream();
        image.Save(stream, encoder);
        stream.Position = 0;
        return stream;
    }
}
