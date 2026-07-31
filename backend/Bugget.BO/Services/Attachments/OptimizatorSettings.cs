using SixLabors.ImageSharp.Formats.Webp;

namespace Bugget.Entities.BO.AttachmentBo;

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

    /// <summary>
    /// Выключает перекодирование видео целиком: оригинал остаётся как есть, превью не строится.
    /// Аварийный тумблер для установки, которой не хватает памяти под ffmpeg.
    /// </summary>
    public bool VideoOptimizationEnabled { get; set; } = true;

    /// <summary>
    /// Сколько ffmpeg-процессов разрешено одновременно на весь процесс приложения.
    /// Умолчание безопасное: один процесс — предсказуемый потолок RSS (MAIN-188).
    /// </summary>
    public int VideoMaxConcurrency { get; set; } = 1;

    /// <summary>Потолок потоков кодировщика (<c>-threads</c> на выходе): каждый поток x264 стоит памяти.</summary>
    public int VideoEncoderThreads { get; set; } = 1;

    /// <summary>
    /// Потолок потоков декодера (<c>-threads</c> на входе). Самый жадный из трёх: на 4K
    /// исходнике frame-threading декодера держит кадровые буферы и вдвое поднимает пик RSS
    /// (замер MAIN-194: 500 MB против 258 MB на одном и том же ролике).
    /// </summary>
    public int VideoDecoderThreads { get; set; } = 1;

    /// <summary>Потолок потоков фильтров (<c>-filter_threads</c>): масштабирование 4K — самый жадный фильтр.</summary>
    public int VideoFilterThreads { get; set; } = 1;

    /// <summary>Потолок времени одного вызова ffmpeg; по истечении убивается всё дерево процессов.</summary>
    public int VideoTimeoutSeconds { get; set; } = 900;
}
