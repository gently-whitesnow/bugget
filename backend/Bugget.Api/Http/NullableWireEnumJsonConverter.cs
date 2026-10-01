using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bugget.Api.Http;

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
