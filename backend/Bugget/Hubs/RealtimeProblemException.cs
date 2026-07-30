using Bugget.Http;
using Microsoft.AspNetCore.SignalR;

namespace Bugget.Hubs;

/// <summary>
/// Единственное исключение, чьё сообщение разрешено отдать клиенту хаба: оно собрано
/// адаптером из дескриптора общего каталога, поэтому <c>code</c> в сокете совпадает с
/// тем, что клиент увидел бы в problem+json на ту же ошибку.
///
/// Тип отдельный, а не базовый <see cref="HubException"/>, именно чтобы граница была
/// fail-closed: фильтр пропускает наружу только его. Сырой <c>HubException</c> —
/// от библиотеки, из чужого кода или написанный по привычке — неотличим от доверенного
/// по типу и потому санитизируется наравне с любым другим исключением. Гарантия задана
/// типом, а не соглашением между местами вызова.
/// </summary>
public sealed class RealtimeProblemException(ProblemDescriptor descriptor)
    : HubException(RealtimeErrorPayload.Create(descriptor));
