using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.Logging;

public sealed class TelegramLoggerProvider : ILoggerProvider
{
    private readonly string _serviceName;
    private readonly TelegramLoggingOptions _options;
    private IExternalScopeProvider? _scopeProvider;

    private readonly HttpClient _http = new();
    private readonly Channel<string> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _senderLoop;

    // Состояние отправки трогает только SenderLoopAsync: читатель очереди один.
    private readonly Random _random = new();
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;
    private TimeSpan _backoff;

    public TelegramLoggerProvider(string serviceName, TelegramLoggingOptions options)
    {
        _serviceName = string.IsNullOrWhiteSpace(serviceName)
            ? throw new ArgumentException("Service name must be provided", nameof(serviceName))
            : serviceName;

        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Очередь: SingleReader, MultiWriter, при переполнении — DropOldest (не блокируем приложение)
        var chOpts = new BoundedChannelOptions(Math.Max(10, _options.MaxQueueSize))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _queue = Channel.CreateBounded<string>(chOpts);

        _senderLoop = Task.Run(SenderLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) =>
        new TelegramLogger(categoryName, _serviceName, _options, _queue, _scopeProvider);

    public void Dispose()
    {
        try
        { _cts.Cancel(); }
        catch { }
        try
        { _queue.Writer.TryComplete(); }
        catch { }
        try
        { _senderLoop.Wait(TimeSpan.FromSeconds(2)); }
        catch { }
        _http.Dispose();
        _cts.Dispose();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    private async Task SenderLoopAsync()
    {
        var endpoint = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
        _backoff = _options.RetryBaseDelay;

        try
        {
            var batch = new List<string>(64);

            while (!_cts.IsCancellationRequested)
            {
                await ReadBatchAsync(batch);

                if (batch.Count == 0)
                {
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                if (now < _nextAllowed)
                {
                    await Task.Delay(_nextAllowed - now, _cts.Token);
                }

                foreach (var payload in BuildPayloads(batch, _options.MaxMessageLength))
                {
                    await SendPayloadAsync(endpoint, payload);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch { /* never throw out of loop */ }
    }

    private async Task ReadBatchAsync(List<string> batch)
    {
        var read = _queue.Reader;
        var batchWindowTask = Task.Delay(_options.BatchWindow, _cts.Token);

        batch.Clear();
        while (await read.WaitToReadAsync(_cts.Token))
        {
            while (read.TryRead(out var line))
            {
                batch.Add(line);
                if (batch.Count >= 200)
                {
                    break;
                }
            }
            break;
        }

        while (!batchWindowTask.IsCompleted && read.TryRead(out var more))
        {
            batch.Add(more);
        }
        try
        { await batchWindowTask; }
        catch { /* ignore */ }
    }

    private async Task SendPayloadAsync(string endpoint, string payload)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = _options.ChatId!,
            ["text"] = payload
        });

        HttpResponseMessage? resp = null;
        try
        {
            resp = await _http.PostAsync(endpoint, content, _cts.Token);

            if ((int)resp.StatusCode == 429)
            {
                // Уважаем retry_after из тела
                var body = await resp.Content.ReadAsStringAsync(_cts.Token);
                var retry = TryParseRetryAfterSeconds(body) ?? 1;
                _nextAllowed = DateTimeOffset.UtcNow.AddSeconds(retry);
                _backoff = _options.RetryBaseDelay;
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                await Task.Delay(NextBackoffDelay(), _cts.Token);
                return;
            }

            _backoff = _options.RetryBaseDelay;
            _nextAllowed = DateTimeOffset.UtcNow + _options.MinDelayBetweenSends;
        }
        catch
        {
            // Не падаем — просто ждём бэкофф и продолжаем
            var delay = NextBackoffDelay();
            try
            { await Task.Delay(delay, _cts.Token); }
            catch { }
        }
        finally
        {
            content.Dispose();
            resp?.Dispose();
        }
    }

    private TimeSpan NextBackoffDelay()
    {
        var jitter = TimeSpan.FromMilliseconds(_backoff.TotalMilliseconds * _random.NextDouble());
        var delay = _backoff + jitter;
        if (delay > _options.RetryMaxDelay)
        {
            delay = _options.RetryMaxDelay;
        }

        var next = TimeSpan.FromMilliseconds(_backoff.TotalMilliseconds * 2);
        _backoff = next <= _options.RetryMaxDelay ? next : _options.RetryMaxDelay;
        return delay;
    }

    private static bool TryExtractRetryAfter(JsonElement root, out int seconds)
    {
        seconds = 0;

        if (root.TryGetProperty("parameters", out var p) &&
            p.ValueKind == JsonValueKind.Object &&
            p.TryGetProperty("retry_after", out var ra) &&
            ra.TryGetInt32(out var s) && s > 0)
        {
            seconds = s;
            return true;
        }

        // Иногда retry_after оказывается прямо в description
        if (root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var text = d.GetString();
            if (text is not null)
            {
                var num = new string(text.Where(char.IsDigit).ToArray());
                if (int.TryParse(num, out var n) && n > 0)
                {
                    seconds = n;
                    return true;
                }
            }
        }
        return false;
    }

    private static int? TryParseRetryAfterSeconds(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryExtractRetryAfter(doc.RootElement, out var s))
            {
                return s;
            }
        }
        catch { /* not json or unexpected */ }
        return null;
    }

    private static IEnumerable<string> BuildPayloads(List<string> lines, int maxLen)
    {
        var sb = new StringBuilder(maxLen + 64);
        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 1 > maxLen)
            {
                if (sb.Length > 0)
                { yield return sb.ToString(); sb.Clear(); }
                if (line.Length > maxLen)
                {
                    var i = 0;
                    while (i < line.Length)
                    {
                        var take = Math.Min(maxLen, line.Length - i);
                        yield return line.AsSpan(i, take).ToString();
                        i += take;
                    }
                    continue;
                }
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(line);
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}
