using Bugget.Domain.Analytics;

namespace Bugget.Application.Services.Analytics;

/// <summary>
/// Маппит wire-ключ периода в полузакрытый интервал <c>[from; to)</c> от текущего времени; <c>all</c> → from = epoch.
/// Полузакрытость (<c>closed_at &lt; to</c>): недавно закрытый репорт попадёт в следующий период, а не двойным счётом.
/// Принимаем <see cref="string"/>, а не enum: без generated-enum со «странными» идентификаторами и кастомного binder.
/// </summary>
public static class PeriodResolver
{
    private static readonly DateTimeOffset EpochUtc =
        new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Допустимые wire-значения — часть контракта: транспорт строит из них причину отказа, не читая текст
    /// исключения. Список и <c>switch</c> ниже обязаны совпадать — за этим следит тест.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedValues = ["7d", "30d", "60d", "180d", "360d", "all"];

    public static PeriodWindow Resolve(string? period, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            throw new ArgumentException(
                $"Period query parameter is required. Allowed: {string.Join(", ", AllowedValues)}.",
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
                $"Unknown period value: '{period}'. Allowed: {string.Join(", ", AllowedValues)}.",
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
