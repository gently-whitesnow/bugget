using Bugget.Entities.Errors;

namespace Users.DA.TeamMembers;

public static class TeamMembersErrors
{
    public static readonly BadRequestError TeamLimitExceededError = new BadRequestError("team_limit_exceeded_error", "Превышен лимит команды");
}
