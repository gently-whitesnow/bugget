import { request } from "./client";
import type { Body, Query, Result } from "./client";

/* ── Поиск по внешним источникам ───────────────────────────────────────────── */

export type ExternalSearchQuery = Query<"/v1/external/search", "get">;
export type ExternalSearchResult = Result<"/v1/external/search", "get">;

/**
 * Страница результатов поиска.
 *
 * Все три параметра уходят в адрес всегда — ровно как их клеил рукописный
 * `URLSearchParams`: контракт объявляет их необязательными, но провод менять
 * нельзя, поэтому значения по умолчанию остаются на call-site.
 */
export const searchExternal = (query: ExternalSearchQuery) =>
  request("/v1/external/search", "get", { query });

export type ApplyExternalSearchResultBody = Body<
  "/v1/external/search/apply",
  "post"
>;

/** Привязка найденного элемента к репорту. Тело ответа пустое. */
export const applyExternalSearchResult = (
  body: ApplyExternalSearchResultBody
) => request("/v1/external/search/apply", "post", { body });
