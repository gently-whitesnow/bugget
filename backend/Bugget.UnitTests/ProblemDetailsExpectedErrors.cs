namespace Bugget.UnitTests;

internal static class ProblemDetailsExpectedErrors
{
    internal static readonly IReadOnlyDictionary<string, ExpectedError> Bugget = Create(
        ("not_found", 404, "Объект не найден"),
        ("report_not_found", 404, "Репорт не найден"),
        ("bug_not_found", 404, "Баг не найден"),
        ("bug_step_not_found", 404, "Шаг бага не найден"),
        ("bug_steps_not_found", 404, "Шаги бага не найдены"),
        ("comment_not_found", 404, "Комментарий не найден"),
        ("bug_steps_order_size_mismatch", 400, "Количество шагов в запросе не совпадает с количеством шагов в базе данных"),
        ("internal_server_error", 500, "Внутренняя ошибка сервера"),
        ("bug_must_have_one_field", 400, "Баг должен содержать хотя бы одно поле"),
        ("attachment_file_not_selected_or_empty", 400, "Файл не выбран или пуст."),
        ("attachment_file_too_large", 400, "Файл превышает допустимый размер 10 МБ."),
        ("attachment_file_extension_not_found", 400, "Расширение файла не найдено."),
        ("attachment_file_name_invalid_chars", 400, "Имя файла содержит недопустимые символы."),
        ("attachment_limit_exceeded", 400, "Превышен лимит количества файлов."),
        ("attachment_type_not_allowed", 400, "Недопустимый тип файла."),
        ("attachment_type_not_supported", 400, "Неподдержанный тип файла: image/test."),
        ("attachment_not_found", 404, "Файл не найден."),
        ("attachment_target_required", 400, "Нужно передать ровно один из параметров: bugId или commentId."),
        ("user_comment_not_found", 404, "Комментарий пользователя не найден."),
        ("report_link_not_found", 404, "Ссылка не найдена."),
        ("team_settings_section_not_found", 404, "Секция настроек команды не найдена."),
        ("workspace_settings_section_not_found", 404, "Секция настроек рабочего пространства не найдена."),
        ("user_settings_section_not_found", 404, "Секция настроек пользователя не найдена."),
        ("workspace_setting_not_found", 400, "Настройка рабочего пространства не найдена."),
        ("workspace_setting_invalid_values", 400, "Неверные значения настройки рабочего пространства."),
        ("team_setting_not_found", 400, "Настройка команды не найдена."),
        ("user_setting_not_found", 400, "Настройка пользователя не найдена."),
        ("organization_id_required", 400, "Идентификатор организации обязателен."),
        ("team_id_required", 400, "Идентификатор команды обязателен."),
        ("idempotency_key_required", 400, "Заголовок Idempotency-Key обязателен."),
        ("workspace_id_required", 400, "workspaceId обязателен."),
        ("creator_user_id_required", 400, "creatorUserId обязателен."),
        ("since_id_required", 400, "sinceId обязателен."),
        ("report_closed", 409, "Репорт закрыт и недоступен для изменений."),
        ("board_ids_max_count_error", 400, "Максимальное количество досок - 10"),
        ("use_report_linking_invalid_values_error", 400, "Значение должно быть одно и быть булевым"),
        ("send_report_link_to_comments_invalid_values_error", 400, "Значение должно быть одно и быть булевым"));

    internal static readonly IReadOnlyDictionary<string, ExpectedError> UsersAndAuthorization = Create(
        ("not_found_error", 404, "Объект не найден"),
        ("team_not_found_error", 404, "Команда не найдена"),
        ("internal_server_error", 500, "Внутренняя ошибка сервера"),
        ("feature_not_implemented", 400, "Функция не реализована"),
        ("paid_feature_not_implemented", 400, "Функция не реализована"),
        ("forbidden_error", 403, "Доступ запрещен"),
        ("self_hosted_mode_error", 403, "Действие не доступно в self-hosted режиме"),
        ("self_hosted_mode_required_error", 403, "Действие доступно только в self-hosted режиме"),
        ("user_already_in_team_error", 400, "Пользователь уже в команде"),
        ("team_max_users_count_error", 400, "Превышено максимальное количество пользователей в команде"),
        ("user_not_in_team_error", 403, "Пользователь не состоит ни в одной команде"),
        ("user_not_found", 404, "User not found"),
        ("expired_refresh_token", 401, "Expired refresh token"),
        ("invalid_refresh_token", 401, "Invalid refresh token"),
        ("invalid_access_token", 401, "Invalid access token"),
        ("expired_access_token", 401, "Expired access token"),
        ("invalid_token", 401, "Invalid token"),
        ("user_not_active", 401, "User not active"),
        ("team_limit_exceeded_error", 400, "Превышен лимит команды"),
        ("teams_count_limit_exceeded_error", 400, "Превышен лимит команд"),
        ("workspace_limit_exceeded_error", 400, "Превышен лимит воркспейса"));

    private static IReadOnlyDictionary<string, ExpectedError> Create(
        params (string Code, int Status, string Title)[] errors) =>
        errors.ToDictionary(
            error => error.Code,
            error => new ExpectedError(error.Status, error.Title),
            StringComparer.Ordinal);
}

internal sealed record ExpectedError(int Status, string Title);
