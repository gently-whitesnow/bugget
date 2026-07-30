import type { ListReportsResponse, reportsApi } from "@/shared/api";

/**
 * `GET /v1/reports/search` отдаёт ту же форму, что и список репортов, поэтому
 * своего DTO у поиска нет.
 */
export type SearchResponse = ListReportsResponse;

/**
 * Параметры запроса поиска — query операции `Search_SearchReports`. Имена
 * исторически в camelCase: они в URL, конверсию не проходят и менять их нельзя
 * (ADR-0009). Рукописный список имён расходился бы с контрактом молча.
 */
export type SearchRequestQueryParams = reportsApi.SearchReportsQuery;
