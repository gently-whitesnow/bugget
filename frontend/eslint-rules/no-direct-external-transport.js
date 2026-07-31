import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля
 * `external` вне единственной транспортной границы (`src/shared/api/external`).
 *
 * Причина та же, что у reports и users: модуль переведён на операции
 * сгенерированного контракта, и адрес там связан с методом, query, телом и
 * типом ответа.
 *
 * Краснота правила закреплена тестом
 * `src/shared/api/external/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля external вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/external (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectExternalTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/external(\\/|$)",
  message
);
