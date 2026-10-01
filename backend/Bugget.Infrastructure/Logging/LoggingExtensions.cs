using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Bugget.Infrastructure.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Логирование из IConfiguration: секция "Serilog" — если есть, используется Serilog; иначе консоль по секции
    /// "Logging"; "TelegramLoggingOptions" — телеграм, независимо от Serilog.
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

        var tgSection = configuration.GetSection("TelegramLoggingOptions");
        var tgOptions = tgSection.Get<TelegramLoggingOptions>() ?? new TelegramLoggingOptions();

        logging.ClearProviders();

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

        // Телеграм — только если включён и корректно сконфигурирован (независим от Serilog)
        if (tgOptions.IsConfigured)
        {
            logging.AddProvider(new TelegramLoggerProvider(serviceName, tgOptions));
            logging.AddFilter<TelegramLoggerProvider>(null, tgOptions.MinimumLevel);
        }
    }
}
