import type * as reportsApi from "@/shared/api/reports";

/**
 * Формы ответа списка репортов — выведены из операций модуля `reports`
 * (`shared/api/reports`), то есть из `specs/contracts/reports/openapi.yaml`
 * вместе с путём и методом.
 *
 * Один и тот же ответ отдают `GET /v2/reports` и `GET /v1/reports/search`,
 * поэтому обе ручки типизируются отсюда; равенство их форм проверяется тестом
 * операций.
 *
 * Форма списка намеренно уже полной карточки репорта: LIST не загружает ссылки,
 * вложения и шаги, и с MAIN-63 эти ключи в ответе отсутствуют, а не приходят `null`.
 * Обращение к ним у элемента списка — ошибка компиляции.
 *
 * Регистр здесь уже camelCase: тело перекладывает интерсептор
 * (`shared/api/instances/base.ts`), и это учтено в типах операций (ADR-0009).
 */

/** Страница списка репортов: `total` + `reports`. */
export type ListReportsResponse = reportsApi.ListReportsResult;

/** Элемент страницы списка. */
export type ReportListItem = ListReportsResponse["reports"][number];

/** Баг в составе элемента списка: без `attachments` и `steps`. */
export type BugListItem = NonNullable<ReportListItem["bugs"]>[number];
