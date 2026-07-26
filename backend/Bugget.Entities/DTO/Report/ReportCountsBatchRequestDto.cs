using System.Text.Json.Serialization;

namespace Bugget.Entities.DTO.Report;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReportCountsBatchRequestDto
{
    public required ReportCountsScopeDto[] Scopes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReportCountsScopeDto
{
    public required string Key { get; init; }
    public int[]? Statuses { get; init; }
    public string? TeamId { get; init; }
    public short[]? CreatorTypes { get; init; }
}
