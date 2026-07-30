import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля `users`
 * вне единственной транспортной границы (`src/shared/api/users`).
 *
 * Причина та же, что у reports: модуль переведён на операции сгенерированного
 * контракта, и адрес там связан с методом, телом и типом ответа. Отличие в
 * форме адреса — префикс модуля (`/api/users`) добавляет интерсептор инстанса,
 * поэтому руками путь пишется целиком, и правило ловит именно его.
 *
 * Краснота правила закреплена тестом
 * `src/shared/api/users/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля users вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/users (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectUsersTransportOptions = transportBoundaryOptions(
  "^\\/api\\/users\\/v[0-9]",
  message
);
