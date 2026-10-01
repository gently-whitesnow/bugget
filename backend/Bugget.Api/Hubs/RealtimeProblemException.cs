using Bugget.Api.Http;
using Microsoft.AspNetCore.SignalR;

namespace Bugget.Api.Hubs;

/// <summary>
/// Единственное исключение, чьё сообщение разрешено отдать клиенту хаба: оно собрано из дескриптора общего каталога,
/// поэтому <c>code</c> в сокете совпадает с problem+json на ту же ошибку.
/// Тип отдельный, а не <see cref="HubException"/>, чтобы граница была fail-closed: фильтр пропускает только его,
/// а сырой <c>HubException</c> (из библиотеки или чужого кода) санитизируется. Гарантия задана типом, а не соглашением.
/// </summary>
public sealed class RealtimeProblemException(ProblemDescriptor descriptor)
    : HubException(RealtimeErrorPayload.Create(descriptor));
