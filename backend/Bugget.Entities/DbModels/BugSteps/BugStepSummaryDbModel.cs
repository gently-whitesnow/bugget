using Bugget.Entities.DbModels.Attachment;

namespace Bugget.Entities.DbModels.BugSteps;

public sealed class BugStepSummaryDbModel
{
    public required int Id { get; init; }
    public required int BugId { get; init; }
    public required string Text { get; init; }
    public required int StepNumber { get; init; }
    public required string CreatorUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public AttachmentDbModel[]? Attachments { get; set; }
}
