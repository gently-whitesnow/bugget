import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `analytics` вне `src/shared/api/analytics`.
 * Шаблон намеренно узкий: `/v2/reports/{id}/analytics` принадлежит `reports`.
 * Гейт-тест: `src/shared/api/analytics/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля analytics вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/analytics (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectAnalyticsTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/analytics(\\/|$)",
  message
);
