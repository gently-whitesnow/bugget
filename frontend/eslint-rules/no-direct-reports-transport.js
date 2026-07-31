import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля `reports`
 * вне единственной транспортной границы (`src/shared/api/reports`).
 *
 * Модуль переведён на операции сгенерированного контракта: путь, метод, query,
 * тело и тип ответа связаны там в одном месте и выведены из
 * `specs/contracts/reports/openapi.yaml`. Новый `appApi.get("/v2/reports/...")`
 * рядом с моделью снова разводит адрес и контракт, и такое расхождение опять
 * находится в рантайме у заказчика, а не на сборке.
 *
 * Формы вызова, которые закрыты правилом, перечислены в `transport-boundary.js`.
 * Краснота правила закреплена тестом
 * `src/shared/api/reports/transportBoundary.gate.test.ts`.
 *
 * Использование в eslint.config:
 *   "no-restricted-syntax": ["error", ...noDirectReportsTransportOptions]
 */

const message =
  "Путь модуля reports вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/reports (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectReportsTransportOptions = transportBoundaryOptions(
  "^\\/v[12]\\/reports",
  message
);
