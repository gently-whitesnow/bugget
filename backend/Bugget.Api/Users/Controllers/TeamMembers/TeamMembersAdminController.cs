using Bugget.Api.Generated.Users;
using Bugget.Api.Http;
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
    /// <remarks>
    /// Сегмент объявлен строкой канонического Int64 (shared.yaml
    /// <c>Int64String</c>), внутрь уходит <c>long</c>. Ограничения маршрута
    /// у этого пути не было и нет: несвязываемый сегмент и раньше отбивало
    /// связывание модели как 400, и <see cref="WireInt64"/> отвечает тем же
    /// классом ошибки — удаление на «соседнего» участника не уезжает.
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
