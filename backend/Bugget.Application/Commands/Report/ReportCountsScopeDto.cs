using System.Text.Json.Serialization;

namespace Bugget.Application.Commands.Report;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReportCountsScopeDto
{
    public required string Key { get; init; }
    public int[]? Statuses { get; init; }
    public string? TeamId { get; init; }
    public short[]? CreatorTypes { get; init; }
}
