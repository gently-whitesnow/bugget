using Bugget.Api.Extensions;
using Bugget.Api.Http;
using Bugget.Api.Hubs;
using Bugget.Application.Errors;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Bugget.Api.Middlewares;

/// <summary>
/// Граница realtime-канала, закрытая по умолчанию: наружу проходит только payload,
/// собранный адаптером из общего каталога (ADR-0008). Всё остальное — включая сырой
/// <see cref="HubException"/>, чьё сообщение SignalR отдал бы клиенту как есть, —
/// сводится к <c>internal_server_error</c>. Форма не RFC 9457: у сообщения в сокете
/// нет ни Content-Type, ни статуса. Текст исключения остаётся в журнале.
/// </summary>
public class HubExceptionHandlerFilter(ILogger<HubExceptionHandlerFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(context);
        }
        catch (RealtimeProblemException)
        {
            // Единственная доверенная ветка: метод хаба уже собрал payload из
            // дескриптора, и подменять его на internal_server_error значит терять
            // причину, известную вызывающему.
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "P0404")
        {
            throw new RealtimeProblemException(BoErrors.NotFoundError.ToDescriptor());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in hub method {HubMethod}", context.HubMethodName);
            throw new RealtimeProblemException(CommonProblemDescriptors.InternalServerError);
        }
    }
}
