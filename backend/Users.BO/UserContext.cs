using Users.Entities.BO;

namespace Users.BO;

public sealed record UserContext(User User, WorkspaceContext[] Workspaces);

public sealed record WorkspaceContext(
    int WorkspaceId,
    string Role,
    int[] TeamIds
);
