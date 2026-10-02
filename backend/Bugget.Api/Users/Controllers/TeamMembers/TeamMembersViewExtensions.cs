using System.Globalization;
using Bugget.Api.Users.Controllers.Workspaces;
using Bugget.Domain.Users;

namespace Bugget.Api.Users.Controllers.TeamMembers;

public static class TeamMembersViewExtensions
{
    public static TeamMembersView ToView(this TeamMember[] members, int sizeLimit)
    {
        return new TeamMembersView
        {
            Members = members.Select(m => new TeamMemberView
            {
                TeamId = m.TeamId.ToString(CultureInfo.InvariantCulture),
                UserId = m.UserId.ToString(CultureInfo.InvariantCulture),
                CreatedAt = m.CreatedAt
            }).ToArray(),
            SizeLimit = sizeLimit
        };
    }
}
