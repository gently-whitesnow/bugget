using Users.Api.Controllers.Workspaces;

namespace Users.Api.Controllers.Teams;

public sealed class AutocompleteTeamsView
{
    public required IEnumerable<TeamView> Teams { get; init; }
    public required int Total { get; init; }
}
