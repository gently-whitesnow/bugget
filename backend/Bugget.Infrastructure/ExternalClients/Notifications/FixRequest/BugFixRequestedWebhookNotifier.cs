using System.Text;
using System.Text.Json;
using Bugget.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.ExternalClients.Notifications.FixRequest;

/// <summary>
/// HTTP-вебхук раннеру агента. Ошибки доставки логируются и не всплывают:
/// вызов идёт из фоновой очереди, и упавший раннер не должен ронять ни запрос
/// пользователя, ни очередь.
/// </summary>
public sealed class BugFixRequestedWebhookNotifier(
    IHttpClientFactory httpClientFactory,
    ILogger<BugFixRequestedWebhookNotifier> logger) : IBugFixRequestedNotifier
{
    public const string HttpClientName = "bug-fix-request-webhook";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task NotifyAsync(BugFixRequestedPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            // Тело сериализуется заранее и уходит со Content-Length: PostAsJsonAsync
            // стримит JSON с Transfer-Encoding: chunked, а простые вебхук-приёмники
            // (включая пилотный раннер) chunked не разбирают и видят пустое тело.
            using var content = new StringContent(
                JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(string.Empty, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Вебхук fix-request отвечен {StatusCode} для репорта {ReportId}, бага {BugId}",
                    (int)response.StatusCode, payload.ReportId, payload.BugId);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Вебхук fix-request не доставлен для репорта {ReportId}, бага {BugId}",
                payload.ReportId, payload.BugId);
        }
    }
}

/// <summary>Раннер не сконфигурирован: сигнал некому слать, маркер в баге остаётся.</summary>
public sealed class NoOpBugFixRequestedNotifier : IBugFixRequestedNotifier
{
    public Task NotifyAsync(BugFixRequestedPayload payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
