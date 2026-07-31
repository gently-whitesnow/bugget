using Bugget.Domain.Errors;

namespace Bugget.Application.Errors;

public static class BoErrors
{
    public static readonly NotFoundError NotFoundError = new NotFoundError("not_found", "Объект не найден");
    public static readonly NotFoundError ReportNotFoundError = new NotFoundError("report_not_found", "Репорт не найден");
    public static readonly NotFoundError BugNotFoundError = new NotFoundError("bug_not_found", "Баг не найден");
    public static readonly NotFoundError BugStepNotFoundError = new NotFoundError("bug_step_not_found", "Шаг бага не найден");
    public static readonly NotFoundError BugStepsNotFoundError = new NotFoundError("bug_steps_not_found", "Шаги бага не найдены");
    public static readonly NotFoundError CommentNotFoundError = new NotFoundError("comment_not_found", "Комментарий не найден");
    public static readonly BadRequestError BugStepsOrderSizeMismatchError = new BadRequestError("bug_steps_order_size_mismatch", "Количество шагов в запросе не совпадает с количеством шагов в базе данных");
    public static readonly InternalServerError InternalServerError = new InternalServerError("internal_server_error", "Внутренняя ошибка сервера");
    public static readonly BadRequestError BugMustHaveOneField = new BadRequestError("bug_must_have_one_field", "Баг должен содержать хотя бы одно поле");

    public static readonly BadRequestError AttachmentFileNotSelectedOrEmpty = new BadRequestError("attachment_file_not_selected_or_empty", "Файл не выбран или пуст.");
    public static readonly BadRequestError AttachmentFileTooLarge = new BadRequestError("attachment_file_too_large", "Файл превышает допустимый размер 10 МБ.");
    public static readonly BadRequestError AttachmentFileExtensionNotFound = new BadRequestError("attachment_file_extension_not_found", "Расширение файла не найдено.");
    public static readonly BadRequestError AttachmentFileNameInvalidChars = new BadRequestError("attachment_file_name_invalid_chars", "Имя файла содержит недопустимые символы.");
    public static readonly BadRequestError AttachmentLimitExceeded = new BadRequestError("attachment_limit_exceeded", "Превышен лимит количества файлов.");
    public static readonly BadRequestError AttachmentTypeNotAllowed = new BadRequestError("attachment_type_not_allowed", "Недопустимый тип файла.");
    public static BadRequestError AttachmentTypeNotSupported(string trustedMimeType) => new BadRequestError("attachment_type_not_supported", $"Неподдержанный тип файла: {trustedMimeType}.");
    public static readonly NotFoundError AttachmentNotFound = new NotFoundError("attachment_not_found", "Файл не найден.");
    public static readonly BadRequestError AttachmentTargetRequired = new BadRequestError("attachment_target_required", "Нужно передать ровно один из параметров: bugId или commentId.");

    public static readonly NotFoundError UserCommentNotFound = new NotFoundError("user_comment_not_found", "Комментарий пользователя не найден.");
    public static readonly NotFoundError ReportLinkNotFound = new NotFoundError("report_link_not_found", "Ссылка не найдена.");

    public static readonly NotFoundError TeamSettingsProcessorNotFound = new NotFoundError("team_settings_section_not_found", "Секция настроек команды не найдена.");
    public static readonly NotFoundError WorkspaceSettingsProcessorNotFound = new NotFoundError("workspace_settings_section_not_found", "Секция настроек рабочего пространства не найдена.");
    public static readonly NotFoundError UserSettingsProcessorNotFound = new NotFoundError("user_settings_section_not_found", "Секция настроек пользователя не найдена.");

    public static readonly BadRequestError WorkspaceSettingNotFound = new BadRequestError("workspace_setting_not_found", "Настройка рабочего пространства не найдена.");
    public static readonly BadRequestError WorkspaceSettingInvalidValues = new BadRequestError("workspace_setting_invalid_values", "Неверные значения настройки рабочего пространства.");
    public static readonly BadRequestError TeamSettingNotFound = new BadRequestError("team_setting_not_found", "Настройка команды не найдена.");
    public static readonly BadRequestError UserSettingNotFound = new BadRequestError("user_setting_not_found", "Настройка пользователя не найдена.");

    public static readonly BadRequestError OrganizationIdRequired = new BadRequestError("organization_id_required", "Идентификатор организации обязателен.");
    public static readonly BadRequestError TeamIdRequired = new BadRequestError("team_id_required", "Идентификатор команды обязателен.");

    public static readonly BadRequestError IdempotencyKeyRequired = new BadRequestError("idempotency_key_required", "Заголовок Idempotency-Key обязателен.");
    public static readonly BadRequestError WorkspaceIdRequired = new BadRequestError("workspace_id_required", "workspaceId обязателен.");
    public static readonly BadRequestError CreatorUserIdRequired = new BadRequestError("creator_user_id_required", "creatorUserId обязателен.");
    public static readonly BadRequestError SinceIdRequired = new BadRequestError("since_id_required", "sinceId обязателен.");
    public static readonly ConflictError ReportClosedError = new ConflictError("report_closed", "Репорт закрыт и недоступен для изменений.");
}
