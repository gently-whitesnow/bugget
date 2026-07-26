using System.Text;
using Authentication;
using Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Users.BO.TeamInvites;
using Users.DA.Interfaces;
using Users.DA.TeamInvites;
using Users.DA.TeamMembers;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Teams;
using Users.Entities.Options;

namespace Users.BO;

public sealed class TeamInvitesService(
    ITeamInvitesRepository teamInvitesRepository,
    IConfiguration configuration,
    IOptions<TeamsOptions> teamsOptions,
    IOptions<WorkspacesOptions> workspacesOptions,
    ITeamMembersRepository teamsMembersRepository,
    IWorkspaceMembersRepository workspaceMembersRepository,
    IAuthorizationRepository authorizationRepository) : ITeamInvitesService
{
    private readonly string domain =
        (configuration.GetValue<string>("DomainOptions:BaseUrl")
         ?? throw new InvalidOperationException("DomainOptions:BaseUrl is not set"))
        .TrimEnd('/');

    private readonly TeamsOptions _teamsOptions = teamsOptions.Value;
    private readonly WorkspacesOptions _workspacesOptions = workspacesOptions.Value;

    public async Task<(TeamInviteDbModel invite, string link)> CreateTeamInviteAsync(int workspaceId, int teamId)
    {
        var (tokenHash, link, expiresAt) = GenerateInviteData();
        var invite = await teamInvitesRepository.CreateTeamInviteAsync(workspaceId, teamId, tokenHash, expiresAt);
        return (invite, link);
    }

    public Task DeleteTeamInviteAsync(int teamId, int id) =>
        teamInvitesRepository.DeleteTeamInviteAsync(teamId, id);

    public Task<TeamInviteDbModel?> GetTeamInviteAsync(int teamId) =>
        teamInvitesRepository.GetTeamInviteAsync(teamId);

    public async Task<ResultStruct<(TeamInviteDbModel invite, string link)>> UpdateTeamInviteAsync(int teamId, int id)
    {
        var (tokenHash, link, expiresAt) = GenerateInviteData();
        var invite = await teamInvitesRepository.UpdateTeamInviteAsync(teamId, id, tokenHash, expiresAt);

        if (invite is null)
        {
            return TeamInvitesErrors.TeamInviteNotFoundError;
        }

        return (invite, link);
    }

    public async Task<ResultStruct<TeamInviteDbModel>> AcceptTeamInviteAsync(string token, long userId)
    {
        var tokenHash = InviteCryptoHelper.HashToken(token, Encoding.UTF8.GetBytes(_teamsOptions.Pepper));
        var teamInvite = await teamInvitesRepository.AcceptTeamInviteAsync(tokenHash);

        if (teamInvite is null)
        {
            return TeamInvitesErrors.TeamInviteNotFoundError;
        }

        var workspaceMemberResult = await workspaceMembersRepository.CreateWorkspaceMemberAsync(
                userId, teamInvite.WorkspaceId, WorkspaceRole.Member, _workspacesOptions.DefaultSizeLimit);

        if (workspaceMemberResult.HasError)
        {
            return workspaceMemberResult.Error!;
        }

        var teamMemberResult = await teamsMembersRepository.CreateTeamMemberAsync(
            userId, teamInvite.TeamId, _teamsOptions.DefaultSizeLimit);

        await authorizationRepository.InvalidateUserCacheAsync(userId);

        if (teamMemberResult.HasError)
        {
            return teamMemberResult.Error!;
        }

        return teamInvite;
    }

    private (byte[] tokenHash, string link, DateTimeOffset expiresAt) GenerateInviteData()
    {
        var token = InviteCryptoHelper.NewTokenRaw();
        var tokenHash = InviteCryptoHelper.HashToken(token, Encoding.UTF8.GetBytes(_teamsOptions.Pepper));
        var link = $"{domain}/invite?token={token}";
        var expiresAt = DateTimeOffset.UtcNow.Add(_teamsOptions.ExpiresIn);
        return (tokenHash, link, expiresAt);
    }
}
