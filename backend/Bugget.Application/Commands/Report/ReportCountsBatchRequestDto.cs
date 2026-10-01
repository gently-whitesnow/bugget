using System.Text.Json.Serialization;

namespace Bugget.Application.Commands.Report;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReportCountsBatchRequestDto
{
    public required ReportCountsScopeDto[] Scopes { get; init; }
}
