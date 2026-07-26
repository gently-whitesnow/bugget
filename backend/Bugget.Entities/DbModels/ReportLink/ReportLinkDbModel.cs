namespace Bugget.Entities.DbModels.ReportLink;

public sealed class ReportLinkDbModel
{
    public required int Id { get; init; }
    public required int ReportId { get; init; }
    public required string Link { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
