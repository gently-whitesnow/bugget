using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bugget.Api.Http;

/// <summary>
/// JSON-кодек enum'ов провода: читает и пишет строку из <c>enum</c> контракта.
///
/// Фабрика регистрируется глобально — так сериализуются элементы массивов
/// (<c>ReportCountsScope.statuses</c>), у которых генератор конвертер не
/// проставляет. На скалярных свойствах NSwag вешает свой
/// <c>JsonStringEnumConverter&lt;T&gt;</c>, а атрибут сильнее опций, поэтому его
/// подменяет <see cref="UseWireValues"/> — иначе одно и то же значение уходило бы
/// на провод в двух разных формах.
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

    /// <summary>
    /// Модификатор контракта: снимает сгенерированный конвертер со свойств
    /// enum-типов и ставит этот кодек. Ставится один раз на резолвер и
    /// накрывает все DTO модулей, включая вложенные формы.
    /// </summary>
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

internal sealed class WireEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly WireEnumMap Map = WireEnum.Map(typeof(TEnum));

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!Map.TryParse(raw, out var value))
        {
            throw new JsonException(
                $"Ожидалось одно из значений: {Map.AllowedValues}.");
        }

        return (TEnum)value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Map.Format(value));
}

/// <summary>
/// Nullable-обёртка: у необязательных полей PATCH `null` — «не трогать», и эта
/// семантика не должна зависеть от того, какой конвертер стоит на свойстве.
/// </summary>
internal sealed class NullableWireEnumJsonConverter<TEnum> : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    private static readonly WireEnumJsonConverter<TEnum> Inner = new();

    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(TEnum), options);

    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}
