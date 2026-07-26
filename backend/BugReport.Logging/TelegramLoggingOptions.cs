using Microsoft.Extensions.Logging;

namespace BugReport.Logging;

public sealed class TelegramLoggingOptions
{
    /// <summary>Глобально включает/выключает телеграм-логирование.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Bot token от @BotFather.</summary>
    public string? BotToken { get; set; }

    /// <summary>Целевой chat_id (число или "@channel").</summary>
    public string? ChatId { get; set; }

    /// <summary>Минимальный уровень для телеграма (по умолчанию Error).</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Error;

    /// Макс. длина сообщения (Telegram: 4096, оставим запас под техтекст)
    public int MaxMessageLength { get; set; } = 4000;

    /// Размер очереди (при переполнении — дропаем старые)
    public int MaxQueueSize { get; set; } = 1000;

    /// Окно накопления перед отправкой (батчинг)
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(500);

    /// Мин. пауза между отправками в один чат (троттлинг)
    public TimeSpan MinDelayBetweenSends { get; set; } = TimeSpan.FromMilliseconds(900);

    /// Базовая задержка бэкоффа при ошибках (если нет retry_after)
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// Максимальная задержка бэкоффа
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(10);

    internal bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BotToken) &&
        !string.IsNullOrWhiteSpace(ChatId);
}
