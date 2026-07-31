using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.Ports;

public interface ITeamMembersRepository
{
    Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(long userId, int teamId, int sizeLimit);
    Task<TeamMember> CreateTeamMemberAsync(long userId, int teamId);

    Task<TeamMember[]> ListTeamMembersAsync(int teamId);
    Task<TeamMember[]> ListTeamsMemberAsync(long userId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
