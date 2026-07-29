using Bugget.BO.Errors;
using Bugget.Extensions;
using Bugget.Http;
using Bugget.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Bugget.Middlewares;

/// <summary>
/// Граница realtime-канала. Каталог кодов тот же, что у HTTP (ADR-0008), форма — не
/// RFC 9457: у сообщения в сокете нет ни Content-Type, ни статуса. Текст исключения
/// наружу не уходит ни в одной ветке.
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
        catch (HubException)
        {
            // Метод хаба уже собрал payload из дескриптора — подменять его на
            // internal_server_error значит терять причину, известную вызывающему.
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "P0404")
        {
            throw RealtimeHubException.From(BoErrors.NotFoundError.ToDescriptor());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in hub method {HubMethod}", context.HubMethodName);
            throw RealtimeHubException.From(CommonProblemDescriptors.InternalServerError);
        }
    }
}
