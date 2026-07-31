namespace Bugget.Domain.Search;

public sealed class SearchReports
{
    public required string? Query { get; init; }
    public required int[]? ReportStatuses { get; init; }
    public required string[]? UserIds { get; init; }
    public required string? TeamId { get; init; }
    public required string? OrganizationId { get; init; }
    public short[]? CreatorTypes { get; init; }
    public required SortOption Sort { get; init; }
    public required uint Skip { get; init; }
    public required uint Take { get; init; }
}
