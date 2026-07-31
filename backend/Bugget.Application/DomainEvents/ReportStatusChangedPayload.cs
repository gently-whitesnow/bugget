using System.Text.Json.Serialization;

namespace Bugget.Application.DomainEvents;

/// <summary>
/// Payload события <c>bugget.report.status_changed</c>: строковые названия
/// enum-значений <see cref="Bugget.Domain.Reports.ReportStatus"/>.
/// Сериализуется как <c>{ "from_status": "Test", "to_status": "Fix" }</c>.
/// </summary>
public sealed class ReportStatusChangedPayload
{
    [JsonPropertyName("from_status")]
    public required string FromStatus { get; init; }

    [JsonPropertyName("to_status")]
    public required string ToStatus { get; init; }
}
