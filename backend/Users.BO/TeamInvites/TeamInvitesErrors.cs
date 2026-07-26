using Flow.Errors;

namespace Users.BO.TeamInvites;

public static class TeamInvitesErrors
{
    public static readonly NotFoundError TeamInviteNotFoundError = new NotFoundError("team_invite_not_found_error", "Инвайт команды не найден");
    public static readonly BadRequestError TeamLimitExceededError = new BadRequestError("team_limit_exceeded_error", "Превышен лимит команды");
}
