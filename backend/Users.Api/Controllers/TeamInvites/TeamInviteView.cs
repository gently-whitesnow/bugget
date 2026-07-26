using Users.Entities.DbModels.Teams;

namespace Users.Api.Controllers.TeamInvites;

public class TeamCreateInviteView
{
    public int Id { get; set; }
    public required string InviteLink { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
}

public class TeamInviteView
{
    public int Id { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
}

public static class TeamInviteViewExtensions
{
    public static TeamCreateInviteView ToView(this (TeamInviteDbModel invite, string inviteLink) result)
    {
        return new TeamCreateInviteView
        {
            Id = result.invite.Id,
            CreatedAt = result.invite.CreatedAt,
            InviteLink = result.inviteLink,
            ExpiresAt = result.invite.ExpiresAt
        };
    }

    public static TeamInviteView ToView(this TeamInviteDbModel invite)
    {
        return new TeamInviteView
        {
            Id = invite.Id,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt
        };
    }
}
