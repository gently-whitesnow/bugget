using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.BO.TeamInvites;

namespace Users.Api.Controllers.TeamInvites;

[Route("v1/invites")]
[Auth]
public sealed class TeamInvitesController(ITeamInvitesService teamInvitesService) : ApiController
{
    /// <summary>
    /// Принять инвайт
    /// </summary>
    [HttpPost("accept")]
    [ProducesResponseType(typeof(AcceptInviteView), 200)]
    public Task<IActionResult> AcceptTeamInviteAsync([FromBody] AcceptInviteDto dto)
    {
        var userId = User.GetIdentity().Id;
        return teamInvitesService.AcceptTeamInviteAsync(dto.Token, userId).AsActionResultAsync(result => result.ToAcceptView());
    }
}
