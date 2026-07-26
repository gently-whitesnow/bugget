using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Описывает JSON как набор строк «путь: тип» — форму ответа без значений.
/// Именно форма и есть публичный контракт: переименование поля, смена типа
/// (<c>long</c> строкой или числом), исчезновение вложенного объекта видны в диффе,
/// а изменение данных — нет, поэтому снимки не протухают от каждого сида.
/// </summary>
/// <remarks>
/// Элементы массива сливаются в один путь <c>[]</c>: контракт описывает элемент,
/// а не их количество. Пустой массив даёт только строку <c>array</c> — форму
/// элемента в этом случае предъявить нечему, и такие места тесты сидируют данными.
/// </remarks>
internal static class JsonShape
{
    public static string Describe(JsonElement root)
    {
        var shape = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        Walk("$", root, shape);

        return string.Join(
            '\n',
            shape.Select(pair => $"{pair.Key}: {string.Join('|', pair.Value)}"));
    }

    private static void Walk(string path, JsonElement element, SortedDictionary<string, SortedSet<string>> shape)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                Add(shape, path, "object");
                foreach (var property in element.EnumerateObject())
                {
                    Walk($"{path}.{property.Name}", property.Value, shape);
                }

                break;

            case JsonValueKind.Array:
                Add(shape, path, "array");
                foreach (var item in element.EnumerateArray())
                {
                    Walk($"{path}[]", item, shape);
                }

                break;

            default:
                Add(shape, path, ScalarType(element.ValueKind));
                break;
        }
    }

    private static string ScalarType(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "null",
        _ => "undefined",
    };

    private static void Add(SortedDictionary<string, SortedSet<string>> shape, string path, string type)
    {
        if (!shape.TryGetValue(path, out var types))
        {
            types = new SortedSet<string>(StringComparer.Ordinal);
            shape[path] = types;
        }

        types.Add(type);
    }
}
