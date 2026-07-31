import { request } from "./client";
import type { Result } from "./client";

/* ── Сессия ────────────────────────────────────────────────────────────────── */

export type LogoutResult = Result<"/v1/logout", "post">;

/**
 * Выход из системы.
 *
 * Ответ (`redirectUrl`) фронт по-прежнему не читает: куда уводить пользователя
 * после выхода, решает `getPostLogoutRedirectUrl` в `shared/lib/auth`. Переход
 * на адрес из ответа — смена поведения, а этот слайс сохраняет его 1:1;
 * расхождение названо в финальном инвентаре, а не исправлено молча.
 */
export const logout = () => request("/v1/logout", "post", {});
