using Bugget.Application.Commands.Bug;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.Bugs;

public interface IBugsService
{
    Task<(BugSummary? Value, Error? Error)> CreateBugAsync(UserIdentity user, string aliasId, BugDto bug);
    Task<(BugPatchResult? Value, Error? Error)> PatchBugAsync(UserIdentity user, string aliasId, int bugId, BugPatchDto patchDto);
    Task<BugSummary?> GetBugAsync(int reportId, int bugId);
}
