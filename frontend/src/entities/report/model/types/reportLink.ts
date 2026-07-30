import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Ссылка репорта — формы ответа и тела запроса из контракта модуля `reports`
 * (`ReportLink` и `ReportLinkRequest`), выведенные из yaml, а не описанные
 * руками (ADR-0009).
 *
 * В ответе есть `reportId`: раньше рукописный тип его не объявлял, и живое
 * поле провода было не видно из кода.
 */
export type ReportLink = Camelized<components["schemas"]["ReportLink"]>;

export type ReportLinkDto = Camelized<
  components["schemas"]["ReportLinkRequest"]
>;
