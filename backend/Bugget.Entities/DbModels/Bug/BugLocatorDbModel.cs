namespace Bugget.Entities.DbModels.Bug;

public sealed class BugLocatorDbModel
{
    public required int ReportId { get; init; }
    public required string CreatorUserId { get; init; }
    public string? CreatorTeamId { get; init; }
    public string? CreatorOrganizationId { get; init; }
    public required Guid PublicId { get; init; }
    public int? TeamReportId { get; init; }
}
