import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `external` вне `src/shared/api/external`.
 * Гейт-тест: `src/shared/api/external/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля external вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/external (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectExternalTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/external(\\/|$)",
  message
);
