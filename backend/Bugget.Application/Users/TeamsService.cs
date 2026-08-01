using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users;

public sealed class TeamsService(
    ITeamsRepository teamsRepository,
    ITeamMembersRepository teamMembersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<TeamsOptions> teamsOptions,
    IOptions<SelfHostedOptions> selfHostedOptions) : ITeamsService
{
    public Task<Team[]> ListTeamsAsync(int[] workspaceIds)
    {
        return teamsRepository.ListTeamsAsync(workspaceIds);
    }

    public Task<Team[]> ListTeamsAsync(int workspaceId, int[] teamIds)
    {
        return teamsRepository.ListTeamsAsync(workspaceId, teamIds);
    }

    public Task<Team[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take)
    {
        return teamsRepository.AutocompleteTeamsAsync(workspaceId, searchString, skip, take);
    }

    public async Task<(Team? Value, Error? Error)> CreateTeamAsync(int workspaceId, string name, long userId, int? userTeamId)
    {
        Team team;

        if (selfHostedOptions.Value.Enabled)
        {
            team = await teamsRepository.CreateTeamAsync(workspaceId, name);
        }
        else
        {
            var teamResult = await teamsRepository.CreateTeamAsync(workspaceId, name, teamsOptions.Value.DefaultTeamsCountLimit);
            if (teamResult.Error is not null)
            {
                return (null, teamResult.Error);
            }
            team = teamResult.Value!;
        }

        // Если у пользователя нет команды, добавляем его в созданную команду
        if (userTeamId is null)
        {
            await teamMembersRepository.CreateTeamMemberAsync(userId, team.Id, teamsOptions.Value.DefaultSizeLimit);
            await authorizationRepository.InvalidateUserCacheAsync(userId);
        }

        return (team, null);
    }

    public Task<Team> UpdateTeamAsync(int workspaceId, int teamId, string name)
    {
        return teamsRepository.UpdateTeamAsync(workspaceId, teamId, name);
    }

    public Task DeleteTeamAsync(int workspaceId, int teamId)
    {
        return teamsRepository.DeleteTeamAsync(workspaceId, teamId);
    }
}
