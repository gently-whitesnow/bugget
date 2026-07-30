namespace Bugget.Entities.DTO.Report;

public sealed class ReportCountsBatchResponseDto
{
    public required List<ReportCountsItemDto> Counts { get; init; }
}

public sealed class ReportCountsItemDto
{
    public required string Key { get; init; }
    public required long Count { get; init; }
}
