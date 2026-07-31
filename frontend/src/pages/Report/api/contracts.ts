import type { externalApi } from "@/shared/api";

/**
 * Формы поиска по внешним источникам — выведены из операций модуля `external`
 * (`shared/api/external`), то есть из `specs/contracts/external/openapi.yaml`
 * вместе с путём и методом. Рукописного DTO здесь больше нет: второе
 * независимое представление тех же данных расходилось бы с контрактом молча.
 *
 * Регистр здесь уже camelCase: тело перекладывает интерсептор
 * (`shared/api/instances/base.ts`), и это учтено в типах операций (ADR-0009).
 */

/** Страница результатов поиска: `total` + `items`. */
export type ExternalSearchResponse = externalApi.ExternalSearchResult;

/** Элемент внешнего источника. */
export type ExternalSearchItem = ExternalSearchResponse["items"][number];

/** Что и к какому репорту привязать. */
export type ExternalSearchApplyRequest =
  externalApi.ApplyExternalSearchResultBody;
