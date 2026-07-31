using Bugget.Entities.Errors;
using Microsoft.Extensions.Options;
using Users.BO.Ports;
using Users.Entities.BO;
using Users.Entities.Options;

namespace Users.BO.TeamMembers;

public sealed class TeamMembersService(
    ITeamMembersRepository teamMembersRepository,
    IOptions<TeamsOptions> teamsOptions,
    IWorkspaceMembersRepository workspaceMembersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<SelfHostedOptions> selfHostedOptions) : ITeamMembersService
{
    public async Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId)
    {
        if (selfHostedOptions.Value.Enabled)
        {
            var teamMember = await teamMembersRepository.CreateTeamMemberAsync(userId, teamId);
            await authorizationRepository.InvalidateUserCacheAsync(userId);
            return (teamMember, null);
        }

        var teamMemberResult = await teamMembersRepository.CreateTeamMemberAsync(userId, teamId, teamsOptions.Value.DefaultSizeLimit);
        if (teamMemberResult.Error is not null)
        {
            return (null, teamMemberResult.Error);
        }
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return teamMemberResult;
    }

    public Task<TeamMember[]> ListTeamMembersAsync(int teamId)
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
