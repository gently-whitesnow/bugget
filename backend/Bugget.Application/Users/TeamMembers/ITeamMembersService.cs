using Bugget.Domain.Errors;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.TeamMembers;

public interface ITeamMembersService
{
    Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId);
    Task<TeamMember[]> ListTeamMembersAsync(int teamId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
