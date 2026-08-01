using Bugget.Application.Commands.BugStep;
using Bugget.Domain.Bugs;

namespace Bugget.Application.Ports;

public interface IBugStepsDbClient
{
    Task<BugStepSummary> CreateBugStepAsync(string userId, int bugId, BugStepDto createDto);
    Task<BugStepSummary> CreateBugStepAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        BugStepDto createDto);
    Task<BugStepSummary?> DeleteBugStepInternalAsync(int reportId, int bugId, int stepId);
    Task<BugStepSummary> PatchBugStepInternalAsync(int reportId, int bugId, int stepId, BugStepDto patchDto);
    Task<BugStepSummary[]> UpdateBugStepsOrderInternalAsync(int reportId, int bugId, BugStepsOrderDto orderDto);

    Task<BugStepSummary[]> ListBugStepsInternalAsync(int reportId, int bugId);
}
