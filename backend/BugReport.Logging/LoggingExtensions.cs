using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace BugReport.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Минимальная настройка логирования: консоль + опционально телеграм + опционально Serilog.
    /// Всё берётся из IConfiguration:
    ///   - "Serilog" секция — если есть, используется Serilog (Console, TCPSink и т.д. через конфиг)
    ///   - "Logging" секция — для консоли (как в ASP.NET Core из коробки), если Serilog не задан
    ///   - "TelegramLoggingOptions" секция — для телеграма (независимо от Serilog)
    /// </summary>
    public static void AddBugReportLogging(this ILoggingBuilder logging,
        IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name must be provided", nameof(serviceName));
        }

        // 1) Биндим опции телеграма
        var tgSection = configuration.GetSection("TelegramLoggingOptions");
        var tgOptions = tgSection.Get<TelegramLoggingOptions>() ?? new TelegramLoggingOptions();

        // 2) Очищаем провайдеры
        logging.ClearProviders();

        // 3) Serilog — если секция "Serilog" задана в конфигурации
        var serilogSection = configuration.GetSection("Serilog");
        if (serilogSection.Exists())
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            logging.AddSerilog(logger, dispose: true);
        }
        else
        {
            // Фолбек: стандартный Microsoft Console
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
        }

        // 4) Телеграм — только если включён и корректно сконфигурирован (независим от Serilog)
        if (tgOptions.IsConfigured)
        {
            logging.AddProvider(new TelegramLoggerProvider(serviceName, tgOptions));
            logging.AddFilter<TelegramLoggerProvider>(null, tgOptions.MinimumLevel);
        }
    }
}
