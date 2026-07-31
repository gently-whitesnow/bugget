using Bugget.Entities.Errors;

namespace Users.BO.Ports;

public static class WorkspaceMembersErrors
{
    public static readonly BadRequestError WorkspaceLimitExceededError = new BadRequestError("workspace_limit_exceeded_error", "Превышен лимит воркспейса");
}
