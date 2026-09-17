import { transportBoundaryOptions } from "./transport-boundary.js";

/**
 * Запрет прямых HTTP-вызовов путей `users` вне `src/shared/api/users`.
 * Закрыты обе формы адреса: полный `/api/users/v1/...` и без префикса
 * (`/v1/workspaces/...`, легаси `/v1/users/...`) — префикс допишет интерсептор,
 * и гейт только на первую форму оставлял бы обход зелёным.
 * Гейт-тест: `src/shared/api/users/transportBoundary.gate.test.ts`.
 */

const message =
  "Путь модуля users вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/users (операции сгенерированного контракта) — добавьте или используйте операцию там.";

export const noDirectUsersTransportOptions = transportBoundaryOptions(
  "^\\/(api\\/users\\/v[0-9]|v[0-9]+\\/(workspaces|users)(\\/|$))",
  message
);
