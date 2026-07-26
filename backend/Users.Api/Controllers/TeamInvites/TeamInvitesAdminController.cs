using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Users.Api.Controllers.TeamInvites;
using Users.BO.TeamInvites;
using Users.Entities.Options;

namespace Users.Api.Controllers;

[Route("v1/workspaces/{workspaceId}/teams/{teamId}/invites")]
[Auth(Roles = "admin")]
[TeamRequired]
public sealed class TeamInvitesAdminController(ITeamInvitesService teamInvitesService, IOptions<TeamsOptions> options) : ApiController
{
    /// <summary>
    /// Создать инвайт для вступления в команду
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TeamCreateInviteView), 200)]
    public async Task<IActionResult> CreateTeamInviteAsync([FromRoute] int workspaceId, [FromRoute] int teamId)
    {
        var invite = await teamInvitesService.CreateTeamInviteAsync(workspaceId, teamId);
        return Ok(invite.ToView());
    }

    /// <summary>
    /// Перегенерировать инвайт для вступления в команду
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TeamCreateInviteView), 201)]
    public Task<IActionResult> UpdateTeamInviteAsync([FromRoute] int teamId, [FromRoute] int id)
    {
        return teamInvitesService.UpdateTeamInviteAsync(teamId, id)
        .AsActionResultAsync(result => result.ToView(), 201);
    }

    /// <summary>
    /// Получить инвайты команды
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TeamInviteView), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> GetTeamInviteAsync([FromRoute] int teamId)
    {
        var invite = await teamInvitesService.GetTeamInviteAsync(teamId);
        if (invite is null)
        {
            return NoContent();
        }
        return Ok(invite.ToView());
    }

    /// <summary>
    /// Удалить инвайт команды
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    public Task DeleteTeamInviteAsync([FromRoute] int teamId, [FromRoute] int id)
    {
        return teamInvitesService.DeleteTeamInviteAsync(teamId, id);
    }
}
