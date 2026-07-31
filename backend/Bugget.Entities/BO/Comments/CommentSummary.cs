namespace Bugget.Entities.BO.Comments;

public sealed class CommentSummary
{
    public required int Id { get; init; }
    public required int BugId { get; init; }
    public required string Text { get; init; }
    public required string CreatorUserId { get; init; }
    public required int CreatorType { get; init; }
    public required int Audience { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
