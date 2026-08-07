using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Api.Http;
using Bugget.Api.Mappers;
using Bugget.Contracts.Reports.Generated;
using ModelContextProtocol;

namespace Bugget.Api.Mcp;

/// <summary>
/// Провод read-инструментов MCP: сериализация ответа и разбор enum'ов на входе.
///
/// Строки статусов — те же, что у REST, и берутся из той же карты
/// <see cref="WireEnum"/>: свой список литералов здесь разошёлся бы с контрактом
/// на первом же значении, добавленном в OpenAPI (ADR-0013).
/// </summary>
internal static class McpWire
{
    /// <summary>
    /// Ответ читает модель, и каждый лишний байт она оплачивает токеном: отступов
    /// нет, пустые поля не пишутся, кириллица идёт как есть, а не как
    /// <c>\uXXXX</c> — экранированный русский заголовок втрое длиннее себя.
    /// Ослабленное экранирование здесь безопасно: результат уезжает строкой
    /// внутри JSON-RPC, а не в разметку страницы.
    /// </summary>
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

    /// <summary>
    /// Неизвестное значение — отказ с перечислением допустимых, а не тихое
    /// отбрасывание фильтра: молча расширенная выборка выглядит для модели как
    /// правдивый ответ на её вопрос.
    /// </summary>
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
