using Bugget.Domain.Attachments;
using Bugget.Domain.Bugs;
using Bugget.Domain.Comments;

namespace Bugget.Domain.Bugs;

public sealed class Bug
{
    public required int Id { get; init; }
    public required int ReportId { get; init; }
    public string? Title { get; init; }
    public string? Receive { get; init; }
    public string? Expect { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CreatorUserId { get; init; }
    public required int Status { get; init; }
    public required int CreatorType { get; init; }
    public Attachment[]? Attachments { get; set; }
    public Comment[]? Comments { get; set; }
    public BugStepSummary[]? Steps { get; set; }
}
