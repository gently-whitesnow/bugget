using Flow;
using Users.Entities.DbModels.Members;

namespace Users.BO.TeamMembers;

public interface ITeamMembersService
{
    Task<ResultStruct<TeamMemberDbModel>> CreateTeamMemberAsync(int teamId, long userId);
    Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
