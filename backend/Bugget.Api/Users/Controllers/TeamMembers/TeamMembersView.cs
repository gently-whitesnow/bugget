using Bugget.Api.Users.Controllers.Workspaces;
using Bugget.Domain.Users;

namespace Bugget.Api.Users.Controllers.TeamMembers;

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
