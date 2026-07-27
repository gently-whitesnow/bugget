using Authentication;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Generated;
using Users.BO.TeamMembers;

namespace Users.Api.Controllers.TeamMembers;

/// <summary>
/// Управление составом команды. Маршрут приходит из
/// <c>specs/contracts/users/openapi.yaml</c> через
/// <see cref="TeamMembersAdminControllerBase"/>.
/// </summary>
[ApiController]
[Auth(Roles = "admin")]
[TeamRequired]
public sealed class TeamMembersAdminController(ITeamMembersService teamMembersService) : TeamMembersAdminControllerBase
{
    /// <summary>
    /// Удалить участника команды
    /// </summary>
    public override async Task<IActionResult> DeleteTeamMember(
        int workspaceId,
        int teamId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await teamMembersService.DeleteTeamMemberAsync(userId, teamId);
        return Ok();
    }
}
