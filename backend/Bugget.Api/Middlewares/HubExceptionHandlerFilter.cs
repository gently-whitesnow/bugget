using Bugget.Api.Extensions;
using Bugget.Api.Http;
using Bugget.Api.Hubs;
using Bugget.Application.Errors;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Bugget.Api.Middlewares;

/// <summary>
/// Граница realtime-канала, закрытая по умолчанию (ADR-0008): наружу проходит только payload из общего каталога,
/// всё остальное, включая сырой <see cref="HubException"/>, сводится к <c>internal_server_error</c>.
/// Форма не RFC 9457: у сообщения в сокете нет ни Content-Type, ни статуса.
/// </summary>
public class HubExceptionHandlerFilter(ILogger<HubExceptionHandlerFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (RealtimeProblemException)
        {
            // Единственная доверенная ветка: payload уже собран методом хаба из дескриптора,
            // подмена на internal_server_error потеряла бы известную вызывающему причину.
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "P0404")
        {
            throw new RealtimeProblemException(BoErrors.NotFoundError.ToDescriptor());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in hub method {HubMethod}", invocationContext.HubMethodName);
            throw new RealtimeProblemException(CommonProblemDescriptors.InternalServerError);
        }
    }
}
