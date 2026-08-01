using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Application.Users.Options;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Users.BackgroundServices;

public sealed class MattermostBotListener(
    IHttpClientFactory httpClientFactory,
    IOptions<MattermostBotOptions> options,
    ILogger<MattermostBotListener> logger) : BackgroundService
{
    public const string MattermostBotAccessTokenKey = "MATTERMOST_BOT_ACCESS_TOKEN";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(2);

    private readonly string? _botAccessToken =
        Environment.GetEnvironmentVariable(MattermostBotAccessTokenKey);

    private string? _botUserId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var botAccessToken = ResolveBotToken();
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.BaseUrl) || string.IsNullOrWhiteSpace(botAccessToken))
        {
            logger.LogInformation("Mattermost bot listener is disabled");
            return;
        }

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_botUserId is null)
            {
                _botUserId = await GetBotUserIdAsync(stoppingToken);
                if (_botUserId is null)
                {
                    var delay = GetReconnectDelay(attempt);
                    logger.LogWarning("Failed to get bot user ID, retrying in {Delay:F1}s (attempt {Attempt})",
                        delay.TotalSeconds, attempt + 1);
                    await Task.Delay(delay, stoppingToken);
                    attempt++;
                    continue;
                }

                logger.LogInformation("Mattermost bot listener started, bot user ID: {BotUserId}", _botUserId);
            }

            try
            {
                await RunWebSocketLoopAsync(stoppingToken);
                attempt = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var delay = GetReconnectDelay(attempt);
                logger.LogWarning(ex, "WebSocket disconnected, reconnecting in {Delay:F1}s (attempt {Attempt})",
                    delay.TotalSeconds, attempt + 1);
                await Task.Delay(delay, stoppingToken);
                attempt++;
            }
        }
    }

    private static TimeSpan GetReconnectDelay(int attempt)
    {
        var baseSeconds = Math.Min(Math.Pow(2, attempt), MaxReconnectDelay.TotalSeconds);
        var jitter = Random.Shared.NextDouble() * baseSeconds * 0.2;
        return TimeSpan.FromSeconds(baseSeconds + jitter);
    }

    private string? ResolveBotToken() =>
        string.IsNullOrWhiteSpace(_botAccessToken) ? options.Value.BotAccessToken : _botAccessToken;

    private async Task RunWebSocketLoopAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var wsUrl = opts.BaseUrl.TrimEnd('/').Replace("https://", "wss://").Replace("http://", "ws://")
                    + "/api/v4/websocket";

        logger.LogInformation("Connecting to Mattermost WebSocket at {Url}", wsUrl);

        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = KeepAliveInterval;
        ws.Options.SetRequestHeader("Authorization", "Bearer " + ResolveBotToken());

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(ConnectTimeout);
        await ws.ConnectAsync(new Uri(wsUrl), connectCts.Token);

        // Authenticate via WebSocket message
        var authMsg = JsonSerializer.Serialize(new
        {
            seq = 1,
            action = "authentication_challenge",
            data = new { token = ResolveBotToken() }
        });
        await ws.SendAsync(Encoding.UTF8.GetBytes(authMsg), WebSocketMessageType.Text, true, ct);

        logger.LogInformation("Connected and authenticated to Mattermost WebSocket");

        var buffer = new byte[8192];
        var messageBuffer = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            messageBuffer.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                receiveCts.CancelAfter(ReceiveTimeout);
                try
                {
                    result = await ws.ReceiveAsync(buffer, receiveCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning("No data received for {Timeout}, assuming dead connection", ReceiveTimeout);
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                messageBuffer.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
            await HandleEventAsync(json, ct);
        }
    }

    private async Task HandleEventAsync(string json, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventProp) || eventProp.GetString() != "posted")
            {
                return;
            }

            if (!root.TryGetProperty("data", out var data))
            {
                return;
            }

            // Only handle direct messages
            if (data.TryGetProperty("channel_type", out var channelType) && channelType.GetString() != "D")
            {
                return;
            }

            if (!data.TryGetProperty("post", out var postProp))
            {
                return;
            }

            var postJson = postProp.GetString();
            if (postJson is null)
            {
                return;
            }

            var post = JsonSerializer.Deserialize<MmPost>(postJson);
            if (post is null || post.UserId == _botUserId)
            {
                return;
            }

            // Respond with the user's Mattermost ID
            var responseText = $"Ваш Mattermost User ID:\n```\n{post.UserId}\n```\nСкопируйте и вставьте его на странице настроек профиля.";
            await SendMessageAsync(post.ChannelId, responseText, ct);
        }
        catch (JsonException)
        {
            // Ignore malformed messages (e.g. status responses)
        }
    }

    private async Task SendMessageAsync(string channelId, string text, CancellationToken ct)
    {
        try
        {
            var opts = options.Value;
            var botAccessToken = ResolveBotToken();
            var client = httpClientFactory.CreateClient("MattermostBot");
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", botAccessToken);

            var payload = JsonSerializer.Serialize(new { channel_id = channelId, message = text });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v4/posts", content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Mattermost message to channel {ChannelId}", channelId);
        }
    }

    private async Task<string?> GetBotUserIdAsync(CancellationToken ct)
    {
        try
        {
            var opts = options.Value;
            var botAccessToken = ResolveBotToken();
            var client = httpClientFactory.CreateClient("MattermostBot");
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", botAccessToken);

            var response = await client.GetAsync("/api/v4/users/me", ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.RootElement.GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bot user info from Mattermost");
            return null;
        }
    }

    private sealed class MmPost
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}
