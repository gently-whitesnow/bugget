import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля
 * `settings` вне единственной транспортной границы (`src/shared/api/settings`).
 *
 * Модуль перешёл с собственного дескриптора операции на общую границу
 * `shared/api/operation.ts`. Прямой `appApi.request({ url: "/v1/..." })` рядом с
 * моделью страницы снова развёл бы адрес и контракт.
 *
 * В шаблоне перечислены все четыре адреса контракта: список секций и три ручки
 * обновления по уровням. Пути других модулей под него не попадают.
 *
 * Краснота правила закреплена тестом
 * `src/shared/api/settings/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля settings вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/settings (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectSettingsTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/(settings-sections|(workspace|team|user)-settings-sections)(\\/|$)",
  message
);
