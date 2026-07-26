using Bugget.Entities.BO.Analytics;

namespace Bugget.BO.Services.Analytics;

/// <summary>
/// Маппит дискретный ключ периода (wire-форма — простая строка) в полузакрытый
/// интервал <c>[from; to)</c> относительно текущего времени. <c>all</c> → from = epoch.
/// Полузакрытость (<c>closed_at &lt; to</c>) гарантирует, что недавно закрытый
/// репорт попадёт в выборку в следующий период, а не двойным счётом.
///
/// Сознательно принимаем <see cref="string"/>, а не enum: после R6 контракт
/// унифицирован под простую wire-строку (`7d`, `30d`, …, `all`) — это убирает
/// generated-enum со «странными» C#-идентификаторами и кастомный model binder.
/// </summary>
public static class PeriodResolver
{
    private static readonly DateTimeOffset EpochUtc =
        new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Разбирает строковый wire-ключ периода и возвращает окно. На неизвестное
    /// значение / null / whitespace бросает <see cref="ArgumentException"/> —
    /// контроллер ловит её и отдаёт 400.
    /// </summary>
    public static PeriodWindow Resolve(string? period, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            throw new ArgumentException(
                "Period query parameter is required. Allowed: 7d, 30d, 60d, 180d, 360d, all.",
                nameof(period));
        }

        var (days, label) = period switch
        {
            "7d" => (7, "last_7_days"),
            "30d" => (30, "last_30_days"),
            "60d" => (60, "last_60_days"),
            "180d" => (180, "last_180_days"),
            "360d" => (360, "last_360_days"),
            "all" => (-1, "all_time"),
            _ => throw new ArgumentException(
                $"Unknown period value: '{period}'. Allowed: 7d, 30d, 60d, 180d, 360d, all.",
                nameof(period)),
        };

        var from = days < 0
            ? EpochUtc
            : utcNow - TimeSpan.FromDays(days);

        return new PeriodWindow
        {
            From = from,
            To = utcNow,
            Label = label,
        };
    }
}
