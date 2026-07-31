using Users.Entities.Errors;

namespace Users.DA.WorkspaceMembers;

public static class WorkspaceMembersErrors
{
    public static readonly BadRequestError WorkspaceLimitExceededError = new BadRequestError("workspace_limit_exceeded_error", "Превышен лимит воркспейса");
}
