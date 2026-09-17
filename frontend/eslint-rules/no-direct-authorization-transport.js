import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `authorization` вне
 * `src/shared/api/authorization`. Закрыты обе формы адреса: полный
 * `/api/authorization/v1/...` и путь контракта `/v1/logout` (префикс допишет
 * интерсептор). Навигация `window.location.href` — осознанное исключение,
 * см. `scripts/contracts/frontend-api-inventory.mjs`.
 * Гейт-тест: `src/shared/api/authorization/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля authorization вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/authorization (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectAuthorizationTransportOptions = transportBoundaryOptions(
  "^\\/(api\\/authorization\\/v[0-9]|v[0-9]+\\/logout(\\/|$))",
  message
);
