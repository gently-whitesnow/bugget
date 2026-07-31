using Bugget.Entities.Errors;
using Users.Entities.DbModels.Members;

namespace Users.DA.TeamMembers;

public interface ITeamMembersRepository
{
    Task<(TeamMemberDbModel? Value, Error? Error)> CreateTeamMemberAsync(long userId, int teamId, int sizeLimit);
    Task<TeamMemberDbModel> CreateTeamMemberAsync(long userId, int teamId);

    Task<TeamMemberDbModel[]> ListTeamMembersAsync(int teamId);
    Task<TeamMemberDbModel[]> ListTeamsMemberAsync(long userId);
    Task DeleteTeamMemberAsync(long userId, int teamId);
}
