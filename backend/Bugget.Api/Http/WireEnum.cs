using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;

namespace Bugget.Api.Http;

/// <summary>
/// Соответствие «член сгенерированного enum ↔ строка провода».
///
/// Строка провода — ровно то, что стоит в <c>enum</c> контракта: NSwag кладёт её
/// в <see cref="EnumMemberAttribute"/>, но имя CLR-члена получает по своим
/// правилам (<c>tg_beta_tester</c> → <c>Tg_beta_tester</c>). Ни
/// <c>JsonStringEnumConverter</c>, ни стандартное связывание query этот атрибут
/// не читают, поэтому источником имени служит эта карта — одна на весь ввод и
/// вывод HTTP-границы (ADR-0012).
/// </summary>
internal static class WireEnum
{
    private static readonly ConcurrentDictionary<Type, WireEnumMap> Maps = new();

    /// <summary>
    /// Enum контракта — тот, у которого генератор проставил строковые значения.
    /// Числовые enum'ы других модулей карта не трогает.
    /// </summary>
    public static bool IsWireEnum(Type type) => type.IsEnum && Map(type).IsWire;

    public static WireEnumMap Map(Type enumType) => Maps.GetOrAdd(enumType, Build);

    private static WireEnumMap Build(Type enumType)
    {
        var toWire = new Dictionary<object, string>();
        var fromWire = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var wire = field.GetCustomAttribute<EnumMemberAttribute>()?.Value;
            if (wire is null)
            {
                continue;
            }

            var value = field.GetValue(null)!;
            toWire[value] = wire;
            fromWire[wire] = value;
        }

        return new WireEnumMap(toWire, fromWire);
    }
}

/// <summary>
/// Карта одного enum'а. Разбор строгий: регистр значим, числовая форма и
/// неизвестная строка не принимаются — «почти подходящее» значение молча стать
/// валидным не должно.
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
    /// Наружу отдаётся только известное значение: число, которого нет в контракте,
    /// — расхождение хранилища с проводом, и подменять его «ближайшим» нельзя.
    /// </summary>
    public string Format(object value) =>
        toWire.TryGetValue(value, out var wire)
            ? wire
            : throw new InvalidOperationException(
                $"Значение {value} типа {value.GetType().Name} не описано в контракте.");
}
