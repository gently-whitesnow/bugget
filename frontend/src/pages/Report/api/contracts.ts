import type { externalApi } from "@/shared/api";

/**
 * Формы поиска по внешним источникам выведены из операций `externalApi`
 * (`specs/contracts/external/openapi.yaml`), рукописных DTO нет.
 * Регистр уже camelCase: тело перекладывает интерсептор (ADR-0009).
 */

/** Страница результатов поиска: `total` + `items`. */
export type ExternalSearchResponse = externalApi.ExternalSearchResult;

export type ExternalSearchItem = ExternalSearchResponse["items"][number];

export type ExternalSearchApplyRequest =
  externalApi.ApplyExternalSearchResultBody;
