using System.Text.Json;
using Bugget.Extensions;
using Bugget.Http;
using Bugget.Hubs;

namespace Bugget.Tests;

/// <summary>
/// Realtime-граница берёт коды из общего каталога, но не притворяется RFC 9457:
/// ни статуса, ни <c>type</c>, ни <c>traceId</c> в payload нет — их некуда деть
/// в сокете, и клиент их там не ищет.
/// </summary>
public sealed class RealtimeErrorPayloadTests
{
    [Fact]
    public void Payload_carries_the_same_code_as_the_http_boundary()
    {
        var descriptor = Bugget.BO.Errors.BoErrors.ReportNotFoundError.ToDescriptor();
        var payload = RealtimeErrorPayload.Create(descriptor);

        using var document = JsonDocument.Parse(payload);

        Assert.Equal(descriptor.Code, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(descriptor.Title, document.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain(
            typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
            typeof(RealtimeError).GetProperties().Select(property => property.PropertyType));
    }

    [Fact]
    public void Payload_does_not_pretend_to_be_rfc_9457()
    {
        var payload = RealtimeErrorPayload.Create(CommonProblemDescriptors.InternalServerError);

        using var document = JsonDocument.Parse(payload);

        Assert.False(document.RootElement.TryGetProperty("type", out _));
        Assert.False(document.RootElement.TryGetProperty("status", out _));
        Assert.False(document.RootElement.TryGetProperty("trace_id", out _));
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void Hub_exception_message_is_the_payload_and_nothing_else()
    {
        var exception = new RealtimeProblemException(CommonProblemDescriptors.Unauthorized);

        Assert.Equal(RealtimeErrorPayload.Create(CommonProblemDescriptors.Unauthorized), exception.Message);
    }
}
