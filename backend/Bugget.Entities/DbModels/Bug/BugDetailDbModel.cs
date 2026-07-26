namespace Bugget.Entities.DbModels.Bug;

public sealed class BugDetailDbModel
{
    public required int BugId { get; init; }
    public required int ReportId { get; init; }
    public int? ReportNumber { get; init; }
    public required int ReportStatus { get; init; }
    public string? Title { get; init; }
    public required int Status { get; init; }
    public required int CreatorType { get; init; }
    public required string CreatorUserId { get; init; }
    public string? Receive { get; init; }
    public string? Expect { get; init; }
    public required int AttachmentsCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
