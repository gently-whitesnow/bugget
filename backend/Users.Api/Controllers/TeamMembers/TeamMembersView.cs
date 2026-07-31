using Users.Api.Controllers.Workspaces;
using Users.Entities.BO;

namespace Users.Api.Controllers.TeamMembers;

public sealed class TeamMembersView
{
    public required TeamMemberView[] Members { get; set; }
    public required int SizeLimit { get; set; }
}

public static class TeamMembersViewExtensions
{
    public static TeamMembersView ToView(this TeamMember[] members, int sizeLimit)
    {
        return new TeamMembersView
        {
            Members = members.Select(m => new TeamMemberView
            {
                TeamId = m.TeamId.ToString(),
                UserId = m.UserId.ToString(),
                CreatedAt = m.CreatedAt
            }).ToArray(),
            SizeLimit = sizeLimit
        };
    }
}
