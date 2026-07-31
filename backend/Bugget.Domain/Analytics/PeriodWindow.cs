namespace Bugget.Domain.Analytics;

/// <summary>
/// Полузакрытый интервал времени [From; To) с человекочитаемым ярлыком.
/// Возвращается из <c>PeriodResolver.Resolve</c>; пробрасывается из BO в Controller.
/// </summary>
public sealed class PeriodWindow
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required string Label { get; init; }
}
