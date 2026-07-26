namespace Bugget.Entities.DTO.Report;

public sealed class ReportCountsBatchResponseDto
{
    public required Dictionary<string, long> Counts { get; init; }
}
