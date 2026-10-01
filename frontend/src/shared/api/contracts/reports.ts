import type * as reportsApi from "@/shared/api/reports";

/**
 * Формы списка репортов выведены из операций модуля `reports`; их же отдаёт
 * `GET /v1/reports/search`. Форма намеренно уже карточки: с MAIN-63 ссылок,
 * вложений и шагов в LIST нет вовсе. Регистр уже camelCase (ADR-0009).
 */

export type ListReportsResponse = reportsApi.ListReportsResult;

export type ReportListItem = ListReportsResponse["reports"][number];

/** Баг в составе элемента списка: без `attachments` и `steps`. */
export type BugListItem = NonNullable<ReportListItem["bugs"]>[number];
