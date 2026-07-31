using Bugget.Entities.Errors;

namespace Users.BO;

public static class BoErrors
{
    public static readonly NotFoundError NotFoundError = new NotFoundError("not_found_error", "Объект не найден");
    public static readonly NotFoundError TeamNotFoundError = new NotFoundError("team_not_found_error", "Команда не найдена");
    public static readonly InternalServerError InternalServerError = new InternalServerError("internal_server_error", "Внутреняя ошибка сервера");
    public static readonly BadRequestError FeatureNotImplemented = new BadRequestError("feature_not_implemented", "Функция не реализована");
    public static readonly BadRequestError PaidFeatureNotImplemented = new BadRequestError("paid_feature_not_implemented", "Функция не реализована");
    public static readonly ForbiddenError ForbiddenError = new ForbiddenError("forbidden_error", "Доступ запрещен");
    public static readonly ForbiddenError SelfHostedModeError = new ForbiddenError("self_hosted_mode_error", "Действие не доступно в self-hosted режиме");
    public static readonly ForbiddenError SelfHostedModeRequiredError = new ForbiddenError("self_hosted_mode_required_error", "Действие доступно только в self-hosted режиме");
    public static readonly BadRequestError UserAlreadyInTeamError = new BadRequestError("user_already_in_team_error", "Пользователь уже в команде");
    public static readonly BadRequestError TeamMaxUsersCountError = new BadRequestError("team_max_users_count_error", "Превышено максимальное количество пользователей в команде");
    public static readonly ForbiddenError UserNotInTeamError = new ForbiddenError("user_not_in_team_error", "Пользователь не состоит ни в одной команде");
}
