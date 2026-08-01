using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Commands.Bug;
using Bugget.Application.Services.Bugs;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Controllers.Bugs;

/// <summary>
/// Api для работы с багами. Маршруты, тела запросов и формы ответов приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="BugsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class BugsController(BugsService bugsService) : BugsControllerBase
{
    public override Task<ActionResult<BugSummary>> CreateBug(
        string aliasId,
        BugRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var createDto = new BugDto
        {
            Title = body.Title,
            Receive = body.Receive,
            Expect = body.Expect
        };

        return bugsService.CreateBugAsync(user, aliasId, createDto)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToSummaryContract(), 201);
    }

    public override Task<ActionResult<BugPatchResult>> PatchBug(
        string aliasId,
        int bugId,
        BugPatchRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var patchDto = new BugPatchDto
        {
            Title = body.Title,
            Receive = body.Receive,
            Expect = body.Expect,
            Status = body.Status
        };

        return bugsService.PatchBugAsync(user, aliasId, bugId, patchDto)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract());
    }
}
