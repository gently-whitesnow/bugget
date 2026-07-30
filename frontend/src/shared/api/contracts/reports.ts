import type { components, operations } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Формы ответа списка репортов — выведены из контракта модуля `reports`.
 *
 * Один и тот же ответ отдают `GET /v2/reports` и `GET /v1/reports/search`
 * (см. `ReportList` в `specs/contracts/reports/openapi.yaml`), поэтому обе ручки
 * типизируются отсюда.
 *
 * Форма списка намеренно уже полной карточки репорта: LIST не загружает ссылки,
 * вложения и шаги, и с MAIN-63 эти ключи в ответе отсутствуют, а не приходят `null`.
 * Обращение к ним у элемента списка — ошибка компиляции.
 *
 * `Camelized` учитывает case-conversion интерсептор (`shared/api/instances/base.ts`):
 * провод — snake_case, код читает camelCase.
 */
export type ReportListItem = Camelized<components["schemas"]["ReportListItem"]>;

/** Баг в составе элемента списка: без `attachments` и `steps`. */
export type BugListItem = Camelized<components["schemas"]["BugListItem"]>;

/** Страница списка репортов: `total` + `reports`. */
export type ListReportsResponse = Camelized<
  components["schemas"]["ReportList"]
>;

/**
 * Query обеих ручек, отдающих список: фильтры и пагинация.
 *
 * Берутся из `operations[...]` напрямую, без `Camelized`: имена query
 * исторически в camelCase, конверсию они не проходят и принадлежат публичному
 * контракту (ADR-0009). Живут в `shared`, потому что список запрашивают два
 * потребителя — `entities/report` и `entities/report-list`.
 */
export type ListReportsQuery = NonNullable<
  operations["Reports_ListReports"]["parameters"]["query"]
>;

/** Query полнотекстового поиска — та же форма ответа, свои фильтры. */
export type SearchReportsQuery = NonNullable<
  operations["Search_SearchReports"]["parameters"]["query"]
>;
