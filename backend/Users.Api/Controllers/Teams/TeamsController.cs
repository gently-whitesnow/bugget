using System.ComponentModel.DataAnnotations;
using Authentication;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Controllers.Teams;
using Users.Api.Controllers.Workspaces;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.Interfaces;

namespace Users.Api.Controllers;

/// <summary>
/// Чтение команд рабочего пространства. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="TeamsControllerBase"/>.
/// </summary>
[ApiController]
[Auth]
public sealed class TeamsController(ITeamsService teamsService) : TeamsControllerBase
{
    /// <summary>
    /// Получение команд по массиву id
    /// </summary>
    [WorkspaceRequired]
    public override async Task<ActionResult<ICollection<TeamSummary>>> ListTeams(
        int workspaceId,
        [MinLength(1)][MaxLength(1000)] IEnumerable<string> body,
        CancellationToken cancellationToken = default)
    {
        var parsedIds = body
            .Where(id => int.TryParse(id, out _))
            .Select(int.Parse)
            .ToArray();
        if (parsedIds.Length == 0)
        {
            return new List<TeamSummary>();
        }

        var teams = await teamsService.ListTeamsAsync(workspaceId, parsedIds);
        return teams.Select(t => new TeamView
        {
            Id = t.Id.ToString(),
            Name = t.Name,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).Select(UsersContractMapper.ToContract).ToList();
    }

    /// <summary>
    /// Поиск команд по имени в текущем workspace
    /// </summary>
    /// <remarks>
    /// Диапазоны skip/take объявлены здесь: генератор minimum/maximum
    /// query-параметров в атрибуты не переносит.
    /// </remarks>
    [WorkspaceRequired]
    public override async Task<ActionResult<AutocompleteTeams>> AutocompleteTeams(
        int workspaceId,
        string? searchString = null,
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(1, 100)] int? take = 10,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (user.WorkspaceId != workspaceId)
        {
            return Forbid();
        }

        var teams = await teamsService.AutocompleteTeamsAsync(
            workspaceId,
            searchString ?? string.Empty,
            skip ?? 0,
            take ?? 10);

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
        }.ToContract());
    }
}
