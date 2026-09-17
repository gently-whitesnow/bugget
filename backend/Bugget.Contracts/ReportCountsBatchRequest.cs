using System.Text.Json.Serialization;

namespace Bugget.Contracts.Reports.Generated;

// В контракте `additionalProperties: false`, но NSwag лишь не генерирует JsonExtensionData — System.Text.Json
// неизвестные поля молча игнорирует. Partial возвращает Disallow → 400: счётчики строятся по ключам запроса,
// и опечатка в имени фильтра обязана быть ошибкой, а не тихо посчитанным «не тем» числом.
// Файл лежит вне Generated/ и генератором не перетирается.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public partial class ReportCountsBatchRequest;
