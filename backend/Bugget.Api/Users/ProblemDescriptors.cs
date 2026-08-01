using Bugget.Api.Http;
using Microsoft.AspNetCore.Http;

namespace Bugget.Api.Users;

public static class ProblemDescriptors
{
    public static readonly ProblemDescriptor LastLoginMethod = new("last_login_method", "Нельзя отвязать единственный способ входа", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InvalidSourceUserId = new("invalid_source_user_id", "Некорректный sourceUserId", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor SameSourceUser = new("same_source_user", "Нельзя объединить аккаунт сам с собой", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor SourceNotFound = new("source_not_found", "Исходный аккаунт не найден", StatusCodes.Status404NotFound);
    public static readonly ProblemDescriptor SourceOwnsWorkspaces = new("source_owns_workspaces", "Исходный аккаунт владеет рабочими пространствами", StatusCodes.Status409Conflict);
    public static readonly ProblemDescriptor MergeFailed = new("merge_failed", "Не удалось объединить аккаунты", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InvalidMattermostUserId = new("invalid_mattermost_user_id", "Некорректный Mattermost User ID", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor AvatarTooLarge = new("avatar_too_large", "Размер файла не должен превышать 200 КБ", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor AvatarFormatNotAllowed = new("avatar_format_not_allowed", "Недопустимый формат файла. Разрешены: JPEG, PNG, GIF, WebP", StatusCodes.Status400BadRequest);
}
