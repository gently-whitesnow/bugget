using System.Text.Json;

namespace Bugget.Api.Http;

/// <param name="Code">Стабильный машинный код — тот же, что в HTTP problem+json.</param>
/// <param name="Title">Заголовок класса ошибки. Текста исключения здесь нет и быть не может.</param>
public sealed record RealtimeError(string Code, string Title);
