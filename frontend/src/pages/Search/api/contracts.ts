import type { ListReportsResponse } from "@/shared/api";

/**
 * `GET /v1/reports/search` отдаёт ту же форму, что и список репортов
 * (`ReportList` в контракте модуля `reports`), поэтому своего DTO у поиска нет.
 */
export type SearchResponse = ListReportsResponse;

/**
 * Параметры запроса поиска. Имена исторически в camelCase — они в URL,
 * менять их нельзя (см. ADR-0005).
 */
export type SearchRequestQueryParams = {
  query?: string;
  reportStatuses?: number[];
  userId?: string;
  teamId?: string;
  sort?: string;
  skip?: number;
  take?: number;
};
