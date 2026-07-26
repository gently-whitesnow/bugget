namespace Bugget.Entities.DTO.Internal;

public sealed class InternalCreateBugResponseDto
{
    public required int ReportId { get; init; }
    public required int BugId { get; init; }
}
