using Bugget.BO.Services.Bugs;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DTO.BugStep;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Bugs;

/// <summary>
/// Api для работы с шагами воспроизведения бага
/// </summary>
[Route("/v2/reports/{aliasId}/bugs/{bugId}/steps")]
public sealed class BugStepsController(BugStepsService bugStepsService) : ApiController
{
    /// <summary>
    /// Добавить шаг
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(BugStepSummaryDbModel), 201)]
    public async Task<IActionResult> CreateBugStepAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromBody] BugStepDto createDto)
    {
        var user = User.GetIdentity();
        return await bugStepsService.CreateBugStepAsync(user, aliasId, bugId, createDto).AsActionResultAsync(201);
    }

    /// <summary>
    /// Удалить шаг
    /// </summary>
    /// <returns></returns>
    [HttpDelete("{stepId}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteBugStepAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int stepId)
    {
        var user = User.GetIdentity();
        return bugStepsService.DeleteBugStepAsync(user, aliasId, bugId, stepId).AsActionResultAsync();
    }

    /// <summary>
    /// Изменить шаг
    /// </summary>
    /// <returns></returns>
    [HttpPatch("{stepId}")]
    [ProducesResponseType(typeof(BugStepSummaryDbModel), 200)]
    public Task<IActionResult> UpdateBugStepAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int stepId, [FromBody] BugStepDto patchDto)
    {
        var user = User.GetIdentity();
        return bugStepsService.PatchBugStepAsync(user, aliasId, bugId, stepId, patchDto).AsActionResultAsync();
    }

    /// <summary>
    /// Изменить порядок шагов
    /// </summary>
    /// <returns></returns>
    [HttpPut("order")]
    [ProducesResponseType(typeof(BugStepSummaryDbModel[]), 200)]
    public Task<IActionResult> UpdateBugStepsOrderAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromBody] BugStepsOrderDto orderDto)
    {
        var user = User.GetIdentity();
        return bugStepsService.UpdateBugStepsOrderAsync(user, aliasId, bugId, orderDto).AsActionResultAsync();
    }
}
