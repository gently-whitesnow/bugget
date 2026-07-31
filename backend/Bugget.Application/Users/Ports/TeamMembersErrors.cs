using Bugget.Domain.Errors;

namespace Bugget.Application.Users.Ports;

public static class TeamMembersErrors
{
    public static readonly BadRequestError TeamLimitExceededError = new BadRequestError("team_limit_exceeded_error", "Превышен лимит команды");
}
