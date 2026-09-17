import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `settings` вне `src/shared/api/settings`.
 * В шаблоне все четыре адреса контракта: список секций и три ручки обновления.
 * Гейт-тест: `src/shared/api/settings/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля settings вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/settings (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectSettingsTransportOptions = transportBoundaryOptions(
  "^\\/v[0-9]+\\/(settings-sections|(workspace|team|user)-settings-sections)(\\/|$)",
  message
);
