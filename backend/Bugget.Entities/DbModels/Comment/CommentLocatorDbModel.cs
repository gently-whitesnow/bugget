namespace Bugget.Entities.DbModels.Comment;

public sealed class CommentLocatorDbModel
{
    public required int CommentId { get; init; }
    public required int BugId { get; init; }
    public required int ReportId { get; init; }
    public required string CreatorUserId { get; init; }
    public string? CreatorTeamId { get; init; }
    public string? CreatorOrganizationId { get; init; }
    public required Guid PublicId { get; init; }
    public int? TeamReportId { get; init; }
}
