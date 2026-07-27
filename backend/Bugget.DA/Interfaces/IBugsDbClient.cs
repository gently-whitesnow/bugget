using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DTO.Bug;

namespace Bugget.DA.Interfaces;

public interface IBugsDbClient
{
    Task<BugSummaryDbModel> CreateBugAsync(string userId, int reportId, BugDto bugDto);

    Task<BugSummaryDbModel> CreateBugAsync(
        ITransactionScope scope,
        string userId,
        int reportId,
        BugDto bugDto);

    Task<BugPatchResultDbModel> PatchBugAsync(int reportId, int bugId, BugPatchDto patchDto);

    Task<BugPatchResultDbModel> PatchBugAsync(
        ITransactionScope scope,
        int reportId,
        int bugId,
        BugPatchDto patchDto);

    Task<BugSummaryDbModel?> GetBugAsync(int reportId, int bugId);

    Task<BugSummaryDbModel?> GetBugAsync(ITransactionScope scope, int reportId, int bugId);

}
