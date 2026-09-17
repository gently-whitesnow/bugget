using Microsoft.AspNetCore.Http;

namespace Bugget.Api.Http;

/// <summary>
/// Дескрипторы ошибок самой границы (валидация, аутентификация, маршрутизация, необработанное исключение).
/// Один на класс ошибки и не зависит от транспорта; модули своих кодов на эти классы не заводят (ADR-0008).
/// </summary>
public static class CommonProblemDescriptors
{
    public static readonly ProblemDescriptor ModelStateValidation = new("model_state_validation_error", "Ошибка валидации модели запроса", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor BadRequest = new("bad_request", "Некорректный запрос", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor Unauthorized = new("unauthorized", "Требуется аутентификация", StatusCodes.Status401Unauthorized);
    public static readonly ProblemDescriptor Forbidden = new("forbidden", "Доступ запрещён", StatusCodes.Status403Forbidden);
    // Совпадает с доменным «объект не найден» намеренно: у клиента это одна ветка, два кода — два каталога.
    public static readonly ProblemDescriptor NotFound = new("not_found", "Объект не найден", StatusCodes.Status404NotFound);
    public static readonly ProblemDescriptor MethodNotAllowed = new("method_not_allowed", "Метод не поддерживается", StatusCodes.Status405MethodNotAllowed);
    public static readonly ProblemDescriptor UnsupportedMediaType = new("unsupported_media_type", "Неподдерживаемый тип содержимого", StatusCodes.Status415UnsupportedMediaType);
    public static readonly ProblemDescriptor InternalServerError = new("internal_server_error", "Внутренняя ошибка сервера", StatusCodes.Status500InternalServerError);

    private static readonly Dictionary<int, ProblemDescriptor> ByStatus = new()
    {
        [StatusCodes.Status400BadRequest] = BadRequest,
        [StatusCodes.Status401Unauthorized] = Unauthorized,
        [StatusCodes.Status403Forbidden] = Forbidden,
        [StatusCodes.Status404NotFound] = NotFound,
        [StatusCodes.Status405MethodNotAllowed] = MethodNotAllowed,
        [StatusCodes.Status415UnsupportedMediaType] = UnsupportedMediaType,
        [StatusCodes.Status500InternalServerError] = InternalServerError,
    };

    /// <summary>
    /// Для ответа фреймворка, у которого есть только статус. Незнакомый статус получает
    /// <c>http_&lt;status&gt;</c>: без стабильного кода клиенту разбирать ответ нечем.
    /// </summary>
    public static ProblemDescriptor ForStatus(int status) =>
        ByStatus.TryGetValue(status, out var descriptor)
            ? descriptor
            : new ProblemDescriptor(
                $"http_{status}",
                status >= StatusCodes.Status500InternalServerError ? InternalServerError.Title : "Ошибка обработки запроса",
                status);
}
