using Bugget.Entities.BO.AttachmentBo;

namespace Bugget.Entities.BO.Bugs;

public sealed class BugStepSummary
{
    public required int Id { get; init; }
    public required int BugId { get; init; }
    public required string Text { get; init; }
    public required int StepNumber { get; init; }
    public required string CreatorUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public Attachment[]? Attachments { get; set; }
}
