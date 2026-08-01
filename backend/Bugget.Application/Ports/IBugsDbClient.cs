using Bugget.Application.Commands.Bug;
using Bugget.Domain.Bugs;

namespace Bugget.Application.Ports;

public interface IBugsDbClient
{
    Task<BugSummary> CreateBugAsync(string userId, int reportId, BugDto bugDto);

    Task<BugSummary> CreateBugAsync(
        ITransactionScope scope,
        string userId,
        int reportId,
        BugDto bugDto);

    Task<BugPatchResult> PatchBugAsync(int reportId, int bugId, BugPatchDto patchDto);

    Task<BugPatchResult> PatchBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId,
        BugPatchDto patchDto);

    Task<BugSummary?> GetBugAsync(int reportId, int bugId);

    Task<BugSummary?> GetBugAsync(ITransactionScope scope, int reportId, int bugId);

}
