using System.Text.Json;

namespace Bugget.Http;

/// <summary>
/// Ошибка realtime-канала. Тот же дескриптор, что у HTTP-границы, но не RFC 9457:
/// у сообщения в сокете нет ни Content-Type, ни статуса, ни <c>instance</c>, поэтому
/// RFC-механику сюда тащить не за чем. Общее — только каталог кодов: <c>code</c>
/// совпадает с тем, что клиент увидел бы в HTTP-ответе на ту же ошибку (ADR-0008).
/// </summary>
public static class RealtimeErrorPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static string Create(ProblemDescriptor descriptor) =>
        JsonSerializer.Serialize(new RealtimeError(descriptor.Code, descriptor.Title), Options);
}

/// <param name="Code">Стабильный машинный код — тот же, что в HTTP problem+json.</param>
/// <param name="Title">Заголовок класса ошибки. Текста исключения здесь нет и быть не может.</param>
public sealed record RealtimeError(string Code, string Title);
