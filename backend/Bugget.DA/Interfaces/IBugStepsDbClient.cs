using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DTO.BugStep;

namespace Bugget.DA.Interfaces;

public interface IBugStepsDbClient
{
    Task<BugStepSummaryDbModel> CreateBugStepAsync(string userId, int bugId, BugStepDto createDto);
    Task<BugStepSummaryDbModel> CreateBugStepAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        BugStepDto createDto);
    Task<BugStepSummaryDbModel?> DeleteBugStepInternalAsync(int reportId, int bugId, int stepId);
    Task<BugStepSummaryDbModel> PatchBugStepInternalAsync(int reportId, int bugId, int stepId, BugStepDto patchDto);
    Task<BugStepSummaryDbModel[]> UpdateBugStepsOrderInternalAsync(int reportId, int bugId, BugStepsOrderDto orderDto);

    Task<BugStepSummaryDbModel[]> ListBugStepsInternalAsync(int reportId, int bugId);
}
