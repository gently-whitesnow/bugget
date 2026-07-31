using Bugget.Api.Generated.Users;
using Bugget.Api.Users.Authentication;
using Bugget.Application.Users.TeamMembers;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Users.Controllers.TeamMembers;

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
        string workspaceId,
        int teamId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await teamMembersService.DeleteTeamMemberAsync(userId, teamId);
        return Ok();
    }
}
