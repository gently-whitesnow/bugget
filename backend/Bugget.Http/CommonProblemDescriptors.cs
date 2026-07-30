using Microsoft.AspNetCore.Http;

namespace Bugget.Http;

/// <summary>
/// Каталог дескрипторов, которые не принадлежат ни одному прикладному модулю: это
/// ошибки самой границы — валидация модели, отказ аутентификации и авторизации,
/// промах маршрутизации, неподходящий метод или media type, необработанное исключение.
///
/// Дескриптор здесь один на класс ошибки и не зависит от транспорта: HTTP-adapter
/// строит из него RFC 9457, SignalR — свой payload. Модули собственных кодов на эти
/// же классы не заводят (ADR-0008).
/// </summary>
public static class CommonProblemDescriptors
{
    public static readonly ProblemDescriptor ModelStateValidation = new("model_state_validation_error", "Ошибка валидации модели запроса", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor BadRequest = new("bad_request", "Некорректный запрос", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor Unauthorized = new("unauthorized", "Требуется аутентификация", StatusCodes.Status401Unauthorized);
    public static readonly ProblemDescriptor Forbidden = new("forbidden", "Доступ запрещён", StatusCodes.Status403Forbidden);
    // Код и заголовок совпадают с доменным «объект не найден» намеренно: у клиента это
    // одна и та же ветка, а два кода на один класс ошибки — это два каталога.
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
    /// Дескриптор для ответа, который сформировал сам фреймворк и у которого нет ничего,
    /// кроме статуса. Незнакомый статус не остаётся без кода: он получает выводимый из
    /// статуса <c>http_&lt;status&gt;</c>, потому что ответ без стабильного кода клиенту
    /// разбирать нечем.
    /// </summary>
    public static ProblemDescriptor ForStatus(int status) =>
        ByStatus.TryGetValue(status, out var descriptor)
            ? descriptor
            : new ProblemDescriptor(
                $"http_{status}",
                status >= StatusCodes.Status500InternalServerError ? InternalServerError.Title : "Ошибка обработки запроса",
                status);
}
