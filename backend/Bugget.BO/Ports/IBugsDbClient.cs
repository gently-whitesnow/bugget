using Bugget.Entities.BO.Bugs;
using Bugget.Entities.DTO.Bug;

namespace Bugget.BO.Ports;

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
