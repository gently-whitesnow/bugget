using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bugget.Api.Http;

/// <summary>
/// JSON-кодек enum'ов провода. Глобальная фабрика нужна для элементов массивов, где генератор
/// конвертер не ставит. На скалярных свойствах NSwag вешает свой конвертер, а атрибут сильнее опций,
/// поэтому его подменяет <see cref="UseWireValues"/> — иначе значение уходило бы в двух формах.
/// </summary>
internal sealed class WireEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        WireEnum.IsWireEnum(Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlying = Nullable.GetUnderlyingType(typeToConvert);
        var converterType = underlying is null
            ? typeof(WireEnumJsonConverter<>).MakeGenericType(typeToConvert)
            : typeof(NullableWireEnumJsonConverter<>).MakeGenericType(underlying);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    /// <summary>Модификатор контракта: заменяет сгенерированный конвертер enum-свойств этим кодеком.</summary>
    public static void UseWireValues(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (WireEnum.IsWireEnum(underlying))
            {
                property.CustomConverter = Instance.CreateConverter(property.PropertyType, JsonSerializerOptions.Default);
            }
        }
    }

    private static readonly WireEnumJsonConverterFactory Instance = new();
}
