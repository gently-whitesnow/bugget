using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.Logging;

internal sealed class TelegramLogger(
    string category,
    string serviceName,
    TelegramLoggingOptions options,
    Channel<string> queue,
    IExternalScopeProvider? scopeProvider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
        scopeProvider?.Push(state) ?? NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) =>
        options.Enabled && logLevel >= options.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || !options.IsConfigured)
        {
            return;
        }

        string text;
        try
        {
            text = formatter?.Invoke(state, exception) ?? state?.ToString() ?? string.Empty;
        }
        catch
        {
            return; // не ломаем приложение из-за форматтера
        }

        var msg = BuildMessage(serviceName, category, logLevel, text, exception);

        // Пишем неблокирующе; при переполнении — старые вытеснятся (DropOldest)
        queue.Writer.TryWrite(msg);
    }

    private static string BuildMessage(
        string serviceName,
        string category,
        LogLevel level,
        string text,
        Exception? ex)
    {
        var sb = new StringBuilder();
        var ts = DateTimeOffset.Now.ToString("HH:mm:ss");

        sb.Append('[').Append(ts).Append("] ")
          .Append(serviceName.ToUpperInvariant()).Append(' ')
          .Append(Emoji(level)).Append(": ");

        var shortCat = category?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (!string.IsNullOrWhiteSpace(shortCat))
        {
            sb.Append('[').Append(shortCat).Append("] ");
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.Append(text);
        }

        if (ex is not null)
        {
            var exMsg = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            if (exMsg.Length > 200)
            {
                exMsg = exMsg[..200] + "...";
            }

            sb.Append(" | Exception: ").Append(ex.GetType().Name).Append(" - ").Append(exMsg);
        }

        return sb.ToString();

        static string Emoji(LogLevel l) => l switch
        {
            LogLevel.Trace => "🔍",
            LogLevel.Debug => "🐛",
            LogLevel.Information => "ℹ️",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            LogLevel.Critical => "🚨",
            _ => "📝"
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
