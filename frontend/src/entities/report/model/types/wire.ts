import type { reportsApi } from "@/shared/api";

/**
 * Формы провода, из которых выводятся сущности стора.
 *
 * Берутся не из «просто схемы» контракта, а из типов операций
 * (`shared/api/reports`): вместе с формой к ним привязаны путь и метод, поэтому
 * смена схемы ответа операции ломает компиляцию здесь. Регистр уже camelCase —
 * тело перекладывает интерсептор (ADR-0009).
 */

/** Ответ `GET /v2/reports/{aliasId}` — карточка репорта целиком. */
export type ReportWire = reportsApi.ReportResult;

/** Баг внутри карточки. */
export type BugWire = NonNullable<ReportWire["bugs"]>[number];

/** Комментарий внутри бага. */
export type CommentWire = NonNullable<BugWire["comments"]>[number];

/** Шаг воспроизведения внутри бага. */
export type BugStepWire = NonNullable<BugWire["steps"]>[number];

/**
 * Вложение — ответ ручки загрузки. Та же форма, что у вложений внутри карточки;
 * равенство проверяется тестом `api/contracts.test.ts`.
 */
export type AttachmentWire = reportsApi.AttachmentResult;

/** Ссылка репорта — ответ ручки создания. */
export type ReportLinkWire = reportsApi.ReportLinkResult;
