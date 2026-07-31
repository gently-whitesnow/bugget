using Bugget.Entities.Errors;

namespace Users.BO.Ports;

public static class TeamsErrors
{
    public static readonly BadRequestError TeamsCountLimitExceededError = new BadRequestError("teams_count_limit_exceeded_error", "Превышен лимит команд");
}
