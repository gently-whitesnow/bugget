using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.BO.Interfaces;
using Users.Entities.DbModels.Teams;
using Users.Entities.Dto.Teams;

namespace Users.Api.Controllers;

[Route("v1/workspaces/{workspaceId}/teams")]
[Auth(Roles = "admin")]
public sealed class TeamsAdminController(ITeamsService teamsService) : ApiController
{
    /// <summary>
    /// Создать команду
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TeamDbModel), 200)]
    [WorkspaceRequired]
    public Task<IActionResult> CreateTeamAsync([FromRoute] int workspaceId, [FromBody] CreateTeamDto createTeamDto)
    {
        var user = User.GetIdentity();

        return teamsService.CreateTeamAsync(workspaceId, createTeamDto.Name, user.Id, user.TeamId).AsActionResultAsync();
    }

    /// <summary>
    /// Обновить команду
    /// </summary>
    [HttpPut("{teamId}")]
    [ProducesResponseType(typeof(TeamDbModel), 200)]
    [WorkspaceRequired]
    public Task<TeamDbModel> UpdateTeamAsync(
        [FromRoute] int workspaceId,
        [FromRoute] int teamId,
        [FromBody] UpdateTeamDto updateTeamDto)
    {
        return teamsService.UpdateTeamAsync(workspaceId, teamId, updateTeamDto.Name);
    }

    /// <summary>
    /// Удалить команду
    /// </summary>
    [HttpDelete("{teamId}")]
    [ProducesResponseType(200)]
    [WorkspaceRequired]
    public Task DeleteTeamAsync([FromRoute] int workspaceId, [FromRoute] int teamId)
    {
        return teamsService.DeleteTeamAsync(workspaceId, teamId);
    }
}
