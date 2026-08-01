using Bugget.Api.Users.Controllers.Workspaces;

namespace Bugget.Api.Users.Controllers.Teams;

public sealed class AutocompleteTeamsView
{
    public required IEnumerable<TeamView> Teams { get; init; }
    public required int Total { get; init; }
}
