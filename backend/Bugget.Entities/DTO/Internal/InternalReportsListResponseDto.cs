namespace Bugget.Entities.DTO.Internal;

public sealed class InternalReportsListResponseDto
{
    public required IReadOnlyList<InternalReportListItemDto> Items { get; init; }
}
