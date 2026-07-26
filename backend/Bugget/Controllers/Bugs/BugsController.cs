using Bugget.BO.Services.Bugs;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DTO.Bug;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Bugs;

/// <summary>
/// Api для работы с багами
/// </summary>
[Route("/v2/reports/{aliasId}/bugs")]
public sealed class BugsController(BugsService bugsService) : ApiController
{
    /// <summary>
    /// Добавить баг
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(BugSummaryDbModel), 201)]
    public Task<IActionResult> CreateBugAsync([FromRoute] string aliasId, [FromBody] BugDto createDto)
    {
        var user = User.GetIdentity();
        return bugsService.CreateBugAsync(user, aliasId, createDto).AsActionResultAsync(201);
    }

    /// <summary>
    /// Изменить баг
    /// </summary>
    /// <returns></returns>
    [HttpPatch("{bugId}")]
    [ProducesResponseType(typeof(BugPatchResultDbModel), 200)]
    public Task<IActionResult> UpdateBugAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromBody] BugPatchDto patchDto)
    {
        var user = User.GetIdentity();
        return bugsService.PatchBugAsync(user, aliasId, bugId, patchDto).AsActionResultAsync();
    }
}
