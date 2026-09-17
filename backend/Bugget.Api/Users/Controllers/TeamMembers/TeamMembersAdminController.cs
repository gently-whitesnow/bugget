using Bugget.Api.Generated.Users;
using Bugget.Api.Http;
using Bugget.Api.Users.Authentication;
using Bugget.Application.Users.TeamMembers;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Users.Controllers.TeamMembers;

/// <summary>Управление составом команды; маршрут — из <c>specs/contracts/users/openapi.yaml</c>.</summary>
[ApiController]
[Auth(Roles = "admin")]
[TeamRequired]
public sealed class TeamMembersAdminController(ITeamMembersService teamMembersService) : TeamMembersAdminControllerBase
{
    /// <summary>Удалить участника команды.</summary>
    /// <remarks>
    /// Сегмент — строка канонического Int64 (shared.yaml <c>Int64String</c>), внутрь уходит <c>long</c>.
    /// Несвязываемый сегмент <see cref="WireInt64"/> отбивает как 400 — удаление на «соседнего» участника не уезжает.
    /// </remarks>
    public override async Task<IActionResult> DeleteTeamMember(
        string workspaceId,
        int teamId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var invalidUserId = WireInt64.TryBindRouteValue(HttpContext, "userId", userId, out var id);
        if (invalidUserId is not null)
        {
            return invalidUserId;
        }

        await teamMembersService.DeleteTeamMemberAsync(id, teamId);
        return Ok();
    }
}
