using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.TeamMembers;

public interface ITeamMembersService
{
    Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId);
    Task<TeamMember[]> ListTeamMembersAsync(int teamId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
