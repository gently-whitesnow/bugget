namespace Bugget.Entities.BO.ReportBo;

public sealed class ReportLink
{
    public required int Id { get; init; }
    public required int ReportId { get; init; }
    public required string Link { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
