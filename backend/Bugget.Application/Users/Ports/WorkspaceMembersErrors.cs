using Bugget.Domain.Errors;

namespace Bugget.Application.Users.Ports;

public static class WorkspaceMembersErrors
{
    public static readonly BadRequestError WorkspaceLimitExceededError = new BadRequestError("workspace_limit_exceeded_error", "Превышен лимит воркспейса");
}
