import type { reportsApi } from "@/shared/api";

/**
 * Формы провода выводятся из типов операций (`shared/api/reports`), а не из
 * схем: смена ответа операции ломает компиляцию здесь. Регистр уже camelCase —
 * тело перекладывает интерсептор (ADR-0009).
 */

export type ReportWire = reportsApi.ReportResult;

export type BugWire = NonNullable<ReportWire["bugs"]>[number];

export type CommentWire = NonNullable<BugWire["comments"]>[number];

export type BugStepWire = NonNullable<BugWire["steps"]>[number];

/** Та же форма, что у вложений в карточке — см. `api/contracts.test.ts`. */
export type AttachmentWire = reportsApi.AttachmentResult;

export type ReportLinkWire = reportsApi.ReportLinkResult;
