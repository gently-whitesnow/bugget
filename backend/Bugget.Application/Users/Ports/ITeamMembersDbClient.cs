using Bugget.Domain.Errors;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface ITeamMembersDbClient
{
    Task<(TeamMember? Value, Error? Error)> CreateTeamMemberAsync(long userId, int teamId, int sizeLimit);
    Task<TeamMember> CreateTeamMemberAsync(long userId, int teamId);

    Task<TeamMember[]> ListTeamMembersAsync(int teamId);
    Task<TeamMember[]> ListTeamsMemberAsync(long userId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
