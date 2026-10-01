using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;

namespace Bugget.Api.Http;

/// <summary>
/// Соответствие «член сгенерированного enum ↔ строка провода» (ADR-0013). NSwag кладёт строку контракта
/// в <see cref="EnumMemberAttribute"/>, а имя CLR-члена строит по своим правилам; ни
/// <c>JsonStringEnumConverter</c>, ни связывание query этот атрибут не читают — источником служит эта карта.
/// </summary>
internal static class WireEnum
{
    private static readonly ConcurrentDictionary<Type, WireEnumMap> Maps = new();

    /// <summary>Enum контракта — со строковыми значениями от генератора; числовые enum'ы других модулей не трогаем.</summary>
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
