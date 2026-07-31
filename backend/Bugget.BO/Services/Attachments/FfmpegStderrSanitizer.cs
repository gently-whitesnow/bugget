using System.Text.RegularExpressions;

namespace Bugget.BO.Services.Attachments;

/// <summary>
/// Приводит stderr ffmpeg к короткой причине без путей и имён файлов. Сырой stderr
/// уезжает и в лог, и в текст исключения фоновой задачи, а внутри него лежат абсолютные
/// пути временного каталога и исходное имя вложения пользователя (MAIN-240). Диагностика
/// при этом нужна: причина отказа ffmpeg остаётся, теряются только идентифицирующие куски.
/// </summary>
public static partial class FfmpegStderrSanitizer
{
    private const string Placeholder = "<path>";
    private const int MaxReasonChars = 300;
    private const int MeaningfulTailLines = 3;

    /// <summary>
    /// Последние строки stderr — там ffmpeg пишет причину отказа; всё, что похоже на путь,
    /// имя файла или адрес в памяти, заменяется плейсхолдером.
    /// </summary>
    public static string Summarize(string stderr, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "no stderr";
        }

        var lines = stderr
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(MeaningfulTailLines);

        var reason = RedactTokens(MaskKnownPaths(string.Join(" | ", lines), arguments));
        return reason.Length > MaxReasonChars ? reason[..MaxReasonChars] + "…" : reason;
    }

    /// <summary>Пути, которые мы сами передали ffmpeg, известны точно — их и имена файлов вырезаем целиком.</summary>
    private static string MaskKnownPaths(string text, IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments.Where(Path.IsPathRooted))
        {
            text = text.Replace(argument, Placeholder, StringComparison.Ordinal);
            var fileName = Path.GetFileName(argument);
            if (!string.IsNullOrEmpty(fileName))
            {
                text = text.Replace(fileName, Placeholder, StringComparison.Ordinal);
            }
        }

        return text;
    }

    /// <summary>
    /// Остаток чистим по форме: токен с разделителем каталогов или с расширением — это путь
    /// или имя файла, а <c>0x...</c> — адрес, который только раздувает кардинальность лога.
    /// </summary>
    private static string RedactTokens(string text)
    {
        var tokens = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => PathLikeToken().IsMatch(token) ? Placeholder : token);

        return HexAddress().Replace(string.Join(' ', tokens), "<addr>");
    }

    [GeneratedRegex(@"[\\/]|\.[A-Za-z0-9]{1,5}\p{P}*$")]
    private static partial Regex PathLikeToken();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex HexAddress();
}
