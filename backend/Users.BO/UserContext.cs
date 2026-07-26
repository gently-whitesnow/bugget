using Users.Entities.DbModels.Users;

namespace Users.BO;

public sealed record UserContext(UserDbModel User, WorkspaceContext[] Workspaces);

public sealed record WorkspaceContext(
    int WorkspaceId,
    string Role,
    int[] TeamIds
);
