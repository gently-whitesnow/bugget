using Bugget.Api.Users.Controllers.Workspaces;
using Bugget.Domain.Users;

namespace Bugget.Api.Users.Controllers.TeamMembers;

public sealed class TeamMembersView
{
    public required TeamMemberView[] Members { get; set; }
    public required int SizeLimit { get; set; }
}
