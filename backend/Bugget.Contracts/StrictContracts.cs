using System.Text.Json.Serialization;

namespace Bugget.Contracts.Reports.Generated;

// Строгость по неизвестным полям тела запроса. В контракте она выражена как
// `additionalProperties: false`, но NSwag переносит это только как «не генерировать
// JsonExtensionData» — System.Text.Json такие поля молча игнорирует.
//
// Эти partial-объявления возвращают поведение, которое было у рукописных DTO
// (JsonUnmappedMemberHandling.Disallow → 400 на неизвестное поле): счётчики
// строятся по ключам из запроса, и опечатка в имени фильтра обязана быть ошибкой,
// а не тихо посчитанным «не тем» числом.
//
// Файл лежит вне Generated/ и генератором не перетирается.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public partial class ReportCountsBatchRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public partial class ReportCountsScope;
