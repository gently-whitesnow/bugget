using Users.Entities.DbModels.Teams;

namespace Users.DA.TeamInvites;

public interface ITeamInvitesRepository
{
    Task<TeamInviteDbModel> CreateTeamInviteAsync(int workspaceId, int teamId, byte[] tokenHash, DateTimeOffset expiresAt);
    Task<TeamInviteDbModel?> UpdateTeamInviteAsync(int teamId, int id, byte[] tokenHash, DateTimeOffset expiresAt);
    Task<TeamInviteDbModel?> GetTeamInviteAsync(int teamId);
    Task DeleteTeamInviteAsync(int teamId, int id);
    Task<TeamInviteDbModel?> AcceptTeamInviteAsync(byte[] tokenHash);
}
