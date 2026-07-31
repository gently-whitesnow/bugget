using SixLabors.ImageSharp.Formats.Webp;

namespace Bugget.Domain.Attachments;

public class OptimizatorSettings
{
    public string FfmpegDirectory { get; set; } = "/ffmpeg";
    public int MaxOriginalWidth { get; set; } = 1440;
    public int MaxPreviewSize { get; set; } = 50;
    public int WebpQuality { get; set; } = 85;
    public WebpFileFormatType FileFormat { get; set; } = WebpFileFormatType.Lossy;
    // качество и скорость посередине
    public WebpEncodingMethod Method { get; set; } = WebpEncodingMethod.Level4;
    public WebpTransparentColorMode TransparentColorMode { get; set; } = WebpTransparentColorMode.Preserve;
    public int VideoMaxWidth { get; set; } = 1280;
    public int VideoCrf { get; set; } = 28;
    public int VideoAudioBitrateKbps { get; set; } = 128;
    public string VideoPreset { get; set; } = "medium";
}
