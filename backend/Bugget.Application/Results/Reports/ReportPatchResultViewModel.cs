namespace Bugget.Application.Results.Reports;

public sealed class ReportPatchResultViewModel
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required int Status { get; init; }
    public required string ResponsibleUserId { get; init; }
    public required string PastResponsibleUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
