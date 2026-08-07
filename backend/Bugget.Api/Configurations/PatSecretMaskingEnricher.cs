using System.Text.RegularExpressions;
using Bugget.Domain.Users;
using Serilog.Core;
using Serilog.Events;

namespace Bugget.Api.Configurations;

/// <summary>
/// Маскирует значение personal access token в строковых свойствах лог-событий:
/// от секрета остаётся открытый префикс, тот же, что виден в списке токенов.
/// Сегодня токен в логи не пишет никто (это держат тесты P1a/P1b), enricher —
/// системная страховка от будущего кода, который залогирует заголовки или тело
/// запроса целиком. Ключ по формату секрета, а не по имени заголовка: токен
/// может утечь в лог не только из Authorization.
/// </summary>
public sealed partial class PatSecretMaskingEnricher : ILogEventEnricher
{
    [GeneratedRegex($"{PersonalAccessTokenSecret.Prefix}[A-Za-z0-9_-]+")]
    private static partial Regex SecretPattern();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is not ScalarValue { Value: string text }
                || !text.Contains(PersonalAccessTokenSecret.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var masked = SecretPattern().Replace(
                text,
                match => match.Value.Length > PersonalAccessTokenSecret.DisplayPrefixLength
                    ? match.Value[..PersonalAccessTokenSecret.DisplayPrefixLength] + "***"
                    : match.Value);

            if (!ReferenceEquals(masked, text))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(masked)));
            }
        }
    }
}
