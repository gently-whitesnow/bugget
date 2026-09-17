using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Api.Http;
using Bugget.Api.Mappers;
using Bugget.Contracts.Reports.Generated;
using ModelContextProtocol;

namespace Bugget.Api.Mcp;

/// <summary>
/// Провод MCP-инструментов: сериализация ответа и разбор enum'ов на входе. Строки статусов берутся из той же карты
/// <see cref="WireEnum"/>, что и у REST: свой список литералов разошёлся бы с OpenAPI-контрактом (ADR-0013).
/// </summary>
internal static class McpWire
{
    // Ответ читает модель и платит токенами за каждый байт: без отступов, без null-полей, кириллица без \uXXXX.
    // Ослабленное экранирование безопасно: результат уезжает строкой внутри JSON-RPC, а не в разметку страницы.
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static string FormatReportStatus(int domainValue) =>
        Format(WireEnumMapper.ToReportStatusWire(domainValue));

    public static string FormatBugStatus(int domainValue) =>
        Format(WireEnumMapper.ToBugStatusWire(domainValue));

    public static string FormatCreatorType(int domainValue) =>
        Format(WireEnumMapper.ToCreatorTypeWire(domainValue));

    public static string FormatAudience(int domainValue) =>
        Format(WireEnumMapper.ToCommentAudienceWire(domainValue));

    public static string FormatAttachType(int domainValue) =>
        Format(WireEnumMapper.ToAttachTypeWire(domainValue));

    public static int[]? ParseReportStatuses(string[]? raw) =>
        Parse<ReportStatus>(raw, "report_statuses", value => value.ToDomainValue());

    public static int ParseReportStatus(string raw) =>
        ParseSingle<ReportStatus>(raw, "status", value => value.ToDomainValue());

    public static int ParseBugStatus(string raw) =>
        ParseSingle<BugStatus>(raw, "status", value => value.ToDomainValue());

    public static int ParseAudience(string raw) =>
        ParseSingle<CommentAudience>(raw, "audience", value => value.ToDomainValue());

    public static int[]? ParseCreatorTypes(string[]? raw) =>
        Parse<CreatorType>(raw, "creator_types", value => value.ToDomainValue());

    private static int ParseSingle<TEnum>(string raw, string parameter, Func<TEnum, int> toDomainValue)
        where TEnum : struct, Enum
    {
        var map = WireEnum.Map(typeof(TEnum));
        if (!map.TryParse(raw, out var parsed))
        {
            throw new McpException(
                $"Параметр {parameter}: значение «{raw}» неизвестно. Допустимые: {map.AllowedValues}.");
        }

        return toDomainValue((TEnum)parsed);
    }

    private static string Format<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        WireEnum.Map(typeof(TEnum)).Format(value);

    // Неизвестное значение — отказ с перечнем допустимых, а не тихий сброс фильтра: молча расширенная выборка
    // выглядит для модели как правдивый ответ.
    private static int[]? Parse<TEnum>(string[]? raw, string parameter, Func<TEnum, int> toDomainValue)
        where TEnum : struct, Enum
    {
        if (raw is null || raw.Length == 0)
        {
            return null;
        }

        var map = WireEnum.Map(typeof(TEnum));
        var values = new int[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            if (!map.TryParse(raw[i], out var parsed))
            {
                throw new McpException(
                    $"Параметр {parameter}: значение «{raw[i]}» неизвестно. Допустимые: {map.AllowedValues}.");
            }

            values[i] = toDomainValue((TEnum)parsed);
        }

        return values;
    }
}
