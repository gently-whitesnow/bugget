using Bugget.Http;
using Microsoft.AspNetCore.SignalR;

namespace Bugget.Hubs;

/// <summary>
/// Единственный способ сообщить клиенту хаба об ошибке. Payload собирает общий адаптер
/// из того же дескриптора, что и HTTP-граница, поэтому <c>code</c> в сокете и в
/// problem+json совпадают. Строкового сообщения у этого исключения нет намеренно: любая
/// строка на границе — это ещё одна форма ошибки и риск опубликовать внутренние детали.
/// </summary>
public static class RealtimeHubException
{
    public static HubException From(ProblemDescriptor descriptor) =>
        new(RealtimeErrorPayload.Create(descriptor));
}
