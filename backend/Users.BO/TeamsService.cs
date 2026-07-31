using Bugget.Entities.Errors;
using Microsoft.Extensions.Options;
using Users.BO.Interfaces;
using Users.DA.Interfaces;
using Users.DA.TeamMembers;
using Users.Entities.DbModels.Teams;
using Users.Entities.Options;

namespace Users.BO;

public sealed class TeamsService(
    ITeamsRepository teamsRepository,
    ITeamMembersRepository teamMembersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<TeamsOptions> teamsOptions,
    IOptions<SelfHostedOptions> selfHostedOptions) : ITeamsService
{
    public Task<TeamDbModel[]> ListTeamsAsync(int[] workspaceIds)
    {
        return teamsRepository.ListTeamsAsync(workspaceIds);
    }

    public Task<TeamDbModel[]> ListTeamsAsync(int workspaceId, int[] teamIds)
    {
        return teamsRepository.ListTeamsAsync(workspaceId, teamIds);
    }

    public Task<TeamDbModel[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take)
    {
        return teamsRepository.AutocompleteTeamsAsync(workspaceId, searchString, skip, take);
    }

    public async Task<(TeamDbModel? Value, Error? Error)> CreateTeamAsync(int workspaceId, string name, long userId, int? userTeamId)
    {
        TeamDbModel team;

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

    public Task<TeamDbModel> UpdateTeamAsync(int workspaceId, int teamId, string name)
    {
        return teamsRepository.UpdateTeamAsync(workspaceId, teamId, name);
    }

    public Task DeleteTeamAsync(int workspaceId, int teamId)
    {
        return teamsRepository.DeleteTeamAsync(workspaceId, teamId);
    }
}
