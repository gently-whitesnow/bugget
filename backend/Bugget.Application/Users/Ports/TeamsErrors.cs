using Bugget.Domain.Errors;

namespace Bugget.Application.Users.Ports;

public static class TeamsErrors
{
    public static readonly BadRequestError TeamsCountLimitExceededError = new BadRequestError("teams_count_limit_exceeded_error", "Превышен лимит команд");
}
