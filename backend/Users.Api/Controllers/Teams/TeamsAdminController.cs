using Authentication;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.Interfaces;

namespace Users.Api.Controllers;

/// <summary>
/// Административные операции над командами. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="TeamsAdminControllerBase"/>.
/// </summary>
[ApiController]
[Auth(Roles = "admin")]
public sealed class TeamsAdminController(ITeamsService teamsService) : TeamsAdminControllerBase
{
    /// <summary>
    /// Создать команду
    /// </summary>
    [WorkspaceRequired]
    public override Task<ActionResult<Team>> CreateTeam(
        int workspaceId,
        TeamCreateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        return teamsService.CreateTeamAsync(workspaceId, body.Name, user.Id, user.TeamId)
            .AsContractResultAsync(HttpContext, model => model.ToContract());
    }

    /// <summary>
    /// Обновить команду
    /// </summary>
    [WorkspaceRequired]
    public override async Task<ActionResult<Team>> UpdateTeam(
        int workspaceId,
        int teamId,
        TeamUpdateRequest body,
        CancellationToken cancellationToken = default)
    {
        var team = await teamsService.UpdateTeamAsync(workspaceId, teamId, body.Name);
        return team.ToContract();
    }

    /// <summary>
    /// Удалить команду
    /// </summary>
    [WorkspaceRequired]
    public override async Task<IActionResult> DeleteTeam(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        await teamsService.DeleteTeamAsync(workspaceId, teamId);
        return Ok();
    }
}
