using Users.Entities.Errors;

namespace Users.DA.Teams;

public static class TeamsErrors
{
    public static readonly BadRequestError TeamsCountLimitExceededError = new BadRequestError("teams_count_limit_exceeded_error", "Превышен лимит команд");
}
