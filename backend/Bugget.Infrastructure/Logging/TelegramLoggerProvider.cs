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
        var nextAllowed = DateTimeOffset.MinValue;
        var rnd = new Random();
        var backoff = _options.RetryBaseDelay;

        try
        {
            var batch = new List<string>(64);
            var read = _queue.Reader;

            while (!_cts.IsCancellationRequested)
            {
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

                if (batch.Count == 0)
                {
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                if (now < nextAllowed)
                {
                    await Task.Delay(nextAllowed - now, _cts.Token);
                }

                var payloads = BuildPayloads(batch, _options.MaxMessageLength);

                foreach (var payload in payloads)
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
                            nextAllowed = DateTimeOffset.UtcNow.AddSeconds(retry);
                            backoff = _options.RetryBaseDelay;
                            continue;
                        }

                        if (!resp.IsSuccessStatusCode)
                        {
                            var jitter = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * rnd.NextDouble());
                            var delay = backoff + jitter;
                            if (delay > _options.RetryMaxDelay)
                            {
                                delay = _options.RetryMaxDelay;
                            }

                            await Task.Delay(delay, _cts.Token);

                            var next = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * 2);
                            backoff = next <= _options.RetryMaxDelay ? next : _options.RetryMaxDelay;

                            continue;
                        }

                        backoff = _options.RetryBaseDelay;
                        nextAllowed = DateTimeOffset.UtcNow + _options.MinDelayBetweenSends;
                    }
                    catch
                    {
                        // Не падаем — просто ждём бэкофф и продолжаем
                        var jitter = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * rnd.NextDouble());
                        var delay = backoff + jitter;
                        if (delay > _options.RetryMaxDelay)
                        {
                            delay = _options.RetryMaxDelay;
                        }

                        try
                        { await Task.Delay(delay, _cts.Token); }
                        catch { }
                        var next = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * 2);
                        backoff = next <= _options.RetryMaxDelay ? next : _options.RetryMaxDelay;
                    }
                    finally
                    {
                        content.Dispose();
                        resp?.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch { /* never throw out of loop */ }
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
