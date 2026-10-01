using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bugget.Api.Http;

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
