using Flow;
using Users.Entities.DbModels.Teams;

namespace Users.BO.TeamInvites;

public interface ITeamInvitesService
{
    Task<(TeamInviteDbModel invite, string link)> CreateTeamInviteAsync(int workspaceId, int teamId);
    Task<ResultStruct<(TeamInviteDbModel invite, string link)>> UpdateTeamInviteAsync(int teamId, int id);
    Task<TeamInviteDbModel?> GetTeamInviteAsync(int teamId);
    Task DeleteTeamInviteAsync(int teamId, int id);
    Task<ResultStruct<TeamInviteDbModel>> AcceptTeamInviteAsync(string token, long userId);
}
