using Bugget.Entities.Errors;
using Users.Entities.DbModels.Members;

namespace Users.BO.TeamMembers;

public interface ITeamMembersService
{
    Task<(TeamMemberDbModel? Value, Error? Error)> CreateTeamMemberAsync(int teamId, long userId);
    Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
