import { request } from "./client";
import type { Body, Query, Result } from "./client";

/* ── Репорт ────────────────────────────────────────────────────────────────── */

export type ListReportsQuery = Query<"/v2/reports", "get">;
export type ListReportsResult = Result<"/v2/reports", "get">;

export const listReports = (query: ListReportsQuery) =>
  request("/v2/reports", "get", { query });

export type CreateReportBody = Body<"/v2/reports", "post">;
export type CreateReportResult = Result<"/v2/reports", "post">;

export const createReport = (body: CreateReportBody) =>
  request("/v2/reports", "post", { body });

export type ReportResult = Result<"/v2/reports/{aliasId}", "get">;

export const getReport = (aliasId: string) =>
  request("/v2/reports/{aliasId}", "get", { path: { aliasId } });

export type PatchReportBody = Body<"/v2/reports/{aliasId}", "patch">;
export type PatchReportResult = Result<"/v2/reports/{aliasId}", "patch">;

export const patchReport = (aliasId: string, body: PatchReportBody) =>
  request("/v2/reports/{aliasId}", "patch", { path: { aliasId }, body });

export type LegacyReportResolveResult = Result<
  "/v2/reports/legacy/{legacyId}",
  "get"
>;

// Контракт объявляет `legacyId` числом, а в адресе страницы он приходит строкой:
// приводим на границе, а не подставляем строку в числовой параметр.
export const resolveLegacyReport = (legacyId: string) =>
  request("/v2/reports/legacy/{legacyId}", "get", {
    path: { legacyId: Number(legacyId) },
  });

export type ReportAnalyticsResult = Result<"/v2/reports/{id}/analytics", "get">;

export const getReportAnalytics = (id: number) =>
  request("/v2/reports/{id}/analytics", "get", { path: { id } });

export type ReportCountsBody = Body<"/v2/reports/counts:batch", "post">;
export type ReportCountsResult = Result<"/v2/reports/counts:batch", "post">;

export const countReportsBatch = (body: ReportCountsBody) =>
  request("/v2/reports/counts:batch", "post", { body });

export type SearchReportsQuery = Query<"/v1/reports/search", "get">;
export type SearchReportsResult = Result<"/v1/reports/search", "get">;

export const searchReports = (query: SearchReportsQuery) =>
  request("/v1/reports/search", "get", { query });
