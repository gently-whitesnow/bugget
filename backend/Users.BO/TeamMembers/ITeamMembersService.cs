using Users.Entities.DbModels.Members;
using Users.Entities.Errors;

namespace Users.BO.TeamMembers;

public interface ITeamMembersService
{
    Task<(TeamMemberDbModel? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId);
    Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
