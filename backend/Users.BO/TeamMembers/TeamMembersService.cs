using Flow;
using Microsoft.Extensions.Options;
using Users.DA.Interfaces;
using Users.DA.TeamMembers;
using Users.Entities.DbModels.Members;
using Users.Entities.Options;

namespace Users.BO.TeamMembers;

public sealed class TeamMembersService(
    ITeamMembersRepository teamMembersRepository,
    IOptions<TeamsOptions> teamsOptions,
    IWorkspaceMembersRepository workspaceMembersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<SelfHostedOptions> selfHostedOptions) : ITeamMembersService
{
    public async Task<ResultStruct<TeamMemberDbModel>> CreateTeamMemberAsync(int teamId, long userId)
    {
        if (selfHostedOptions.Value.Enabled)
        {
            var teamMember = await teamMembersRepository.CreateTeamMemberAsync(userId, teamId);
            await authorizationRepository.InvalidateUserCacheAsync(userId);
            return teamMember;
        }

        var teamMemberResult = await teamMembersRepository.CreateTeamMemberAsync(userId, teamId, teamsOptions.Value.DefaultSizeLimit);
        if (teamMemberResult.HasError)
        {
            return teamMemberResult.Error!;
        }
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return teamMemberResult;
    }

    public Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId)
    {
        return teamMembersRepository.ListTeamMembersAsync(teamId);
    }

    public async Task DeleteTeamMemberAsync(long userId, int teamId)
    {
        await teamMembersRepository.DeleteTeamMemberAsync(userId, teamId);
        await workspaceMembersRepository.DeleteWorkspaceMemberAsync(userId, teamId);
        await authorizationRepository.InvalidateUserCacheAsync(userId);
    }
}
