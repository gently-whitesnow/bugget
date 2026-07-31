import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Доски Kaiten ──────────────────────────────────────────────────────────── */

export type KaitenBoardsResult = Result<"/v1/external/kaiten/boards", "get">;
export type KaitenBoard = KaitenBoardsResult[number];

/**
 * Доски для автокомплита.
 *
 * Без строки фильтра query не уходит вовсе — как и раньше, когда call-site
 * передавал axios пустой объект `params`: у адреса не появлялся хвостовой `?`.
 */
export const searchKaitenBoards = (query?: string) =>
  request(
    "/v1/external/kaiten/boards",
    "get",
    query ? { query: { query } } : {}
  );

export type KaitenBoardsBatchGetBody = Body<
  "/v1/external/kaiten/boards/batch-get",
  "post"
>;

/** Доски по идентификаторам: один запрос вместо N. */
export const batchGetKaitenBoards = (body: KaitenBoardsBatchGetBody) =>
  request("/v1/external/kaiten/boards/batch-get", "post", { body });
