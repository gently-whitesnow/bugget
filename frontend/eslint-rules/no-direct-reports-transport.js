import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `reports` вне `src/shared/api/reports`:
 * прямой `appApi.get("/v2/reports/...")` разводит адрес и контракт
 * (`specs/contracts/reports/openapi.yaml`). Формы вызова — в
 * `transport-boundary.js`.
 * Гейт-тест: `src/shared/api/reports/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля reports вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/reports (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectReportsTransportOptions = transportBoundaryOptions(
  "^\\/v[12]\\/reports",
  message
);
