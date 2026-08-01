using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users.TeamMembers;

public sealed class TeamMembersService(
    ITeamMembersDbClient teamMembersDbClient,
    IOptions<TeamsOptions> teamsOptions,
    IWorkspaceMembersDbClient workspaceMembersDbClient,
    IUserCacheInvalidator userCacheInvalidator,
    IOptions<SelfHostedOptions> selfHostedOptions) : ITeamMembersService
{
    public async Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId)
    {
        if (selfHostedOptions.Value.Enabled)
        {
            var teamMember = await teamMembersDbClient.CreateTeamMemberAsync(userId, teamId);
            await userCacheInvalidator.InvalidateUserCacheAsync(userId);
            return (teamMember, null);
        }

        var teamMemberResult = await teamMembersDbClient.CreateTeamMemberAsync(userId, teamId, teamsOptions.Value.DefaultSizeLimit);
        if (teamMemberResult.Error is not null)
        {
            return (null, teamMemberResult.Error);
        }
        await userCacheInvalidator.InvalidateUserCacheAsync(userId);
        return teamMemberResult;
    }

    public Task<TeamMember[]> ListTeamMembersAsync(int teamId)
    {
        return teamMembersDbClient.ListTeamMembersAsync(teamId);
    }

    public async Task DeleteTeamMemberAsync(long userId, int teamId)
    {
        await teamMembersDbClient.DeleteTeamMemberAsync(userId, teamId);
        await workspaceMembersDbClient.DeleteWorkspaceMemberAsync(userId, teamId);
        await userCacheInvalidator.InvalidateUserCacheAsync(userId);
    }
}
