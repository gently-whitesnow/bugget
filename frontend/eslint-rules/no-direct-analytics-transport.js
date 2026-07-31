import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля
 * `analytics` вне единственной транспортной границы (`src/shared/api/analytics`).
 *
 * Причина та же, что у reports и users: модуль переведён на операции
 * сгенерированного контракта, и адрес там связан с методом, query и типом ответа.
 *
 * Шаблон намеренно узкий — `^/v2/analytics`. Detail по репорту
 * (`/v2/reports/{id}/analytics`) принадлежит модулю `reports` и закрыт его
 * правилом; попади он под оба, сообщение указывало бы на чужую границу.
 *
 * Краснота правила закреплена тестом
 * `src/shared/api/analytics/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля analytics вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/analytics (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectAnalyticsTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/analytics(\\/|$)",
  message
);
