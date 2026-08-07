using Bugget.Api.Configurations;
using Bugget.Domain.Users;
using FluentAssertions;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Bugget.UnitTests.Configurations;

public sealed class PatSecretMaskingEnricherTests
{
    [Fact]
    public void MasksSecretInStringProperty_KeepingDisplayPrefix()
    {
        var generated = PersonalAccessTokenSecret.Generate();
        var logged = Capture(logger =>
            logger.Information("Заголовок запроса: {Authorization}", $"Bearer {generated.Value}"));

        var value = ((ScalarValue)logged.Properties["Authorization"]).Value as string;

        value.Should().NotContain(generated.Value);
        value.Should().Contain(generated.DisplayPrefix + "***");
        // Открытый префикс — ровно то, что и так видно в списке токенов.
        value.Should().Contain("Bearer " + generated.DisplayPrefix);
    }

    [Fact]
    public void LeavesUnrelatedPropertiesUntouched()
    {
        var logged = Capture(logger =>
            logger.Information("Пользователь {UserId} обновил {Count} багов", "user-42", 3));

        ((ScalarValue)logged.Properties["UserId"]).Value.Should().Be("user-42");
        ((ScalarValue)logged.Properties["Count"]).Value.Should().Be(3);
    }

    private static LogEvent Capture(Action<ILogger> write)
    {
        LogEvent? captured = null;
        var logger = new LoggerConfiguration()
            .Enrich.With(new PatSecretMaskingEnricher())
            .WriteTo.Sink(new DelegateSink(logEvent => captured = logEvent))
            .CreateLogger();

        write(logger);

        return captured ?? throw new InvalidOperationException("Событие не записано.");
    }

    private sealed class DelegateSink(Action<LogEvent> onEmit) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => onEmit(logEvent);
    }
}
