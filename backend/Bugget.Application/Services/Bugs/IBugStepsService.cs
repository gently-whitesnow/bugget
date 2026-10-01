using Bugget.Application.Commands.BugStep;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.Bugs;

public interface IBugStepsService
{
    Task<(BugStepSummary? Value, Error? Error)> CreateBugStepAsync(UserIdentity user, string aliasId, int bugId, BugStepDto createDto);
    Task<(BugStepSummary? Value, Error? Error)> CreateBugStepWithAttachmentsAsync(UserIdentity user, string aliasId, int bugId, BugStepDto createDto, IReadOnlyList<AttachmentUpload> files, CancellationToken ct);
    Task<Error?> DeleteBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId);
    Task<(BugStepSummary? Value, Error? Error)> PatchBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId, BugStepDto patchDto);
    Task<(BugStepSummary[]? Value, Error? Error)> UpdateBugStepsOrderAsync(UserIdentity user, string aliasId, int bugId, BugStepsOrderDto orderDto);
}
