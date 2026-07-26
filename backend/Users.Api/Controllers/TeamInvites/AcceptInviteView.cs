using Users.Entities.DbModels.Teams;

namespace Users.Api.Controllers.TeamInvites;

public sealed record AcceptInviteView(int Id, int TeamId, int WorkspaceId);

public static class AcceptInviteViewExtensions
{
    public static AcceptInviteView ToAcceptView(this TeamInviteDbModel invite)
    {
        return new AcceptInviteView(invite.Id, invite.TeamId, invite.WorkspaceId);
    }
}
