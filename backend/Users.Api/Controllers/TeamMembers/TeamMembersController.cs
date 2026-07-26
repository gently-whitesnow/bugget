using System.Net;
using Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Users.BO.TeamMembers;
using Users.Entities.Options;

namespace Users.Api.Controllers.TeamMembers;

[Auth]
[Route("v1/workspaces/{workspaceId}/teams/{teamId}/members")]
[WorkspaceRequired]
public sealed class TeamMembersController(ITeamMembersService teamMembersService, IOptions<TeamsOptions> teamsOptions, IOptions<SelfHostedOptions> selfHostedOptions) : ApiController
{
    /// <summary>
    /// Вступить в команду
    /// </summary>
    /// <returns></returns>
    [HttpPost("join")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public Task JoinTeamAsync([FromRoute] int teamId)
    {
        var user = User.GetIdentity();
        return teamMembersService.CreateTeamMemberAsync(teamId, user.Id);
    }

    /// <summary>
    /// Получить участников команды
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TeamMembersView), (int)HttpStatusCode.OK)]
    public async Task<TeamMembersView> ListTeamMembersAsync([FromRoute] int teamId)
    {
        var members = await teamMembersService.ListTeamMembersAsync(teamId);
        var sizeLimit = selfHostedOptions.Value.Enabled ? 0 : teamsOptions.Value.DefaultSizeLimit;
        return members.ToView(sizeLimit);
    }

    /// <summary>
    /// Выйти из команды
    /// </summary>
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public Task LeaveTeamAsync([FromRoute] int teamId)
    {
        var user = User.GetIdentity();
        return teamMembersService.DeleteTeamMemberAsync(user.Id, teamId);
    }
}
