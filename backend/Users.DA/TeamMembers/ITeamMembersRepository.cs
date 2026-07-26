using Flow;
using Users.Entities.DbModels.Members;

namespace Users.DA.TeamMembers;

public interface ITeamMembersRepository
{
    Task<ResultStruct<TeamMemberDbModel>> CreateTeamMemberAsync(long userId, int teamId, int sizeLimit);
    Task<TeamMemberDbModel> CreateTeamMemberAsync(long userId, int teamId);

    Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId);
    Task<TeamMemberDbModel[]> ListTeamsMemberAsync(long userId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
