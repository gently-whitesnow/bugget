using Bugget.Api.Generated.Reports;
using Bugget.BO.Services.Bugs;
using Bugget.Entities.Authentication;
using Bugget.Entities.DTO.BugStep;
using Bugget.Extensions;
using Bugget.Mappers;
using Bugget.Reports.Contracts.Generated;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Bugs;

/// <summary>
/// Api для работы с шагами воспроизведения бага. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="BugStepsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class BugStepsController(BugStepsService bugStepsService) : BugStepsControllerBase
{
    public override Task<ActionResult<BugStep>> CreateBugStep(
        string aliasId,
        int bugId,
        BugStepRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return bugStepsService.CreateBugStepAsync(user, aliasId, bugId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract(), 201);
    }

    public override Task<ActionResult<BugStep>> PatchBugStep(
        string aliasId,
        int bugId,
        int stepId,
        BugStepRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return bugStepsService.PatchBugStepAsync(user, aliasId, bugId, stepId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract());
    }

    public override Task<ActionResult<ICollection<BugStep>>> UpdateBugStepsOrder(
        string aliasId,
        int bugId,
        BugStepsOrderRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var orderDto = new BugStepsOrderDto { StepIds = body.Step_ids.ToArray() };

        return bugStepsService.UpdateBugStepsOrderAsync(user, aliasId, bugId, orderDto)
            .AsContractResultAsync(HttpContext, ICollection<BugStep> (dbModels) => [.. dbModels.Select(step => step.ToContract())]);
    }

    public override Task<IActionResult> DeleteBugStep(
        string aliasId,
        int bugId,
        int stepId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return bugStepsService.DeleteBugStepAsync(user, aliasId, bugId, stepId).AsActionResultAsync(HttpContext);
    }

    private static BugStepDto ToDto(BugStepRequest body) => new() { Text = body.Text };
}
