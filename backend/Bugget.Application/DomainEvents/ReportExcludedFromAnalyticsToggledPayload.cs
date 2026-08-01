using System.Text.Json.Serialization;

namespace Bugget.Application.DomainEvents;

/// <summary>
/// Payload события <c>bugget.report.excluded_from_analytics_toggled</c>:
/// один булевый флаг исключения отчёта из аналитики.
/// Сериализуется как <c>{ "is_excluded": true }</c>.
/// </summary>
public sealed class ReportExcludedFromAnalyticsToggledPayload
{
    [JsonPropertyName("is_excluded")]
    public required bool IsExcluded { get; init; }
}
