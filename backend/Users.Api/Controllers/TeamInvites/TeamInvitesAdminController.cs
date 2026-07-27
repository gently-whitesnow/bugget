using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Controllers.TeamInvites;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.TeamInvites;

namespace Users.Api.Controllers;

/// <summary>
/// Приглашения в команду. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через
/// <see cref="TeamInvitesAdminControllerBase"/>.
/// </summary>
[ApiController]
[Auth(Roles = "admin")]
[TeamRequired]
public sealed class TeamInvitesAdminController(ITeamInvitesService teamInvitesService) : TeamInvitesAdminControllerBase
{
    /// <summary>
    /// Создать инвайт для вступления в команду
    /// </summary>
    public override async Task<ActionResult<TeamInviteWithLink>> CreateTeamInvite(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var invite = await teamInvitesService.CreateTeamInviteAsync(workspaceId, teamId);
        return Ok(invite.ToView().ToContract());
    }

    /// <summary>
    /// Перегенерировать инвайт для вступления в команду
    /// </summary>
    public override Task<ActionResult<TeamInviteWithLink>> UpdateTeamInvite(
        string workspaceId,
        int teamId,
        int id,
        CancellationToken cancellationToken = default)
    {
        return teamInvitesService.UpdateTeamInviteAsync(teamId, id)
            .AsContractResultAsync(result => result.ToView().ToContract(), 201);
    }

    /// <summary>
    /// Получить инвайты команды
    /// </summary>
    public override async Task<ActionResult<TeamInvite>> GetTeamInvite(
        string workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var invite = await teamInvitesService.GetTeamInviteAsync(teamId);
        if (invite is null)
        {
            return NoContent();
        }

        return Ok(invite.ToView().ToContract());
    }

    /// <summary>
    /// Удалить инвайт команды
    /// </summary>
    public override async Task<IActionResult> DeleteTeamInvite(
        string workspaceId,
        int teamId,
        int id,
        CancellationToken cancellationToken = default)
    {
        await teamInvitesService.DeleteTeamInviteAsync(teamId, id);
        return Ok();
    }
}
