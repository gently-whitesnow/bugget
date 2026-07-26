using System.Net;
using Authentication;
using Microsoft.AspNetCore.Mvc;
using Users.BO.TeamMembers;

namespace Users.Api.Controllers.TeamMembers;

[Auth(Roles = "admin")]
[Route("v1/workspaces/{workspaceId}/teams/{teamId}/members")]
[TeamRequired]
public sealed class TeamMembersAdminController(ITeamMembersService teamMembersService) : ApiController
{
    /// <summary>
    /// Удалить участника команды
    /// </summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public Task DeleteTeamMemberAsync([FromRoute] int teamId, [FromRoute] long userId)
    {
        return teamMembersService.DeleteTeamMemberAsync(userId, teamId);
    }
}
