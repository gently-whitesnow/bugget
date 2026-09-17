using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;

namespace Bugget.Api.Http;

/// <summary>
/// Карта одного enum'а. Разбор строгий: регистр значим, числовая форма и неизвестная строка
/// не принимаются — «почти подходящее» значение молча стать валидным не должно.
/// </summary>
internal sealed class WireEnumMap(
    IReadOnlyDictionary<object, string> toWire,
    IReadOnlyDictionary<string, object> fromWire)
{
    public bool IsWire => fromWire.Count > 0;

    /// <summary>Все значения провода в порядке объявления — для текста ошибки.</summary>
    public string AllowedValues => string.Join(", ", fromWire.Keys);

    public bool TryParse(string? raw, out object value)
    {
        if (raw is not null && fromWire.TryGetValue(raw, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>
    /// Наружу — только известное значение: число вне контракта — расхождение хранилища с проводом,
    /// подменять его «ближайшим» нельзя.
    /// </summary>
    public string Format(object value) =>
        toWire.TryGetValue(value, out var wire)
            ? wire
            : throw new InvalidOperationException(
                $"Значение {value} типа {value.GetType().Name} не описано в контракте.");
}
