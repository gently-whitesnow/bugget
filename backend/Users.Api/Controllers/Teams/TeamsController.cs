using System.ComponentModel.DataAnnotations;
using Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Controllers.Teams;
using Users.Api.Controllers.Workspaces;
using Users.BO.Interfaces;

namespace Users.Api.Controllers;

[Auth]
[Route("v1/workspaces/{workspaceId}/teams")]
public sealed class TeamsController(ITeamsService teamsService) : ApiController
{
    /// <summary>
    /// Получение команд по массиву id
    /// </summary>
    [HttpPost("batch/list")]
    [ProducesResponseType(typeof(TeamView[]), 200)]
    [WorkspaceRequired]
    public async Task<TeamView[]> ListTeamsAsync(
        [FromRoute] int workspaceId,
        [FromBody][MinLength(1)][MaxLength(1000)] string[] teamIds)
    {
        var parsedIds = teamIds
            .Where(id => int.TryParse(id, out _))
            .Select(int.Parse)
            .ToArray();
        if (parsedIds.Length == 0)
        {
            return [];
        }

        var teams = await teamsService.ListTeamsAsync(workspaceId, parsedIds);
        return teams.Select(t => new TeamView
        {
            Id = t.Id.ToString(),
            Name = t.Name,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToArray();
    }

    /// <summary>
    /// Поиск команд по имени в текущем workspace
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(AutocompleteTeamsView), 200)]
    [WorkspaceRequired]
    public async Task<IActionResult> AutocompleteTeamsAsync(
        [FromRoute] int workspaceId,
        [FromQuery] string? searchString = null,
        [FromQuery][Range(0, int.MaxValue)] int skip = 0,
        [FromQuery][Range(1, 100)] int take = 10)
    {
        var user = User.GetIdentity();
        if (user.WorkspaceId != workspaceId)
        {
            return Forbid();
        }

        var teams = await teamsService.AutocompleteTeamsAsync(workspaceId, searchString ?? string.Empty, skip, take);

        return Ok(new AutocompleteTeamsView
        {
            Teams = teams.Select(t => new TeamView
            {
                Id = t.Id.ToString(),
                Name = t.Name,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }),
            Total = teams.Length
        });
    }
}
