import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля
 * `authorization` вне единственной транспортной границы
 * (`src/shared/api/authorization`).
 *
 * Форм адреса, как и у users, две — префикс модуля дописывает интерсептор
 * инстанса, а не call-site:
 *
 *   * полный путь `/api/authorization/v1/...` — интерсептор пропускает его как есть;
 *   * путь контракта `/v1/logout` — интерсептор дописывает префикс сам.
 *
 * Правило закрывает обе: гейт, который краснеет только на первой, оставляет
 * обход границы зелёным.
 *
 * Под правило не попадает браузерная навигация на вход
 * (`window.location.href = "/api/authorization/v1/..."`): это не HTTP-вызов
 * фронта, а переход страницы, и в контракте таких путей нет — осознанное
 * исключение перечислено в `docs/frontend-api-inventory.md`.
 *
 * Краснота правила закреплена тестом
 * `src/shared/api/authorization/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля authorization вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/authorization (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectAuthorizationTransportOptions = transportBoundaryOptions(
  "^\\/(api\\/authorization\\/v[0-9]|v[0-9]+\\/logout(\\/|$))",
  message
);
