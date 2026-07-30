import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Ссылки ────────────────────────────────────────────────────────────────── */

export type ReportLinkBody = Body<"/v2/reports/{aliasId}/links", "post">;
export type ReportLinkResult = Result<"/v2/reports/{aliasId}/links", "post">;

export const createReportLink = (aliasId: string, body: ReportLinkBody) =>
  request("/v2/reports/{aliasId}/links", "post", { path: { aliasId }, body });

export const updateReportLink = (
  aliasId: string,
  linkId: number,
  body: ReportLinkBody
) =>
  request("/v2/reports/{aliasId}/links/{linkId}", "put", {
    path: { aliasId, linkId },
    body,
  });

export const deleteReportLink = (aliasId: string, linkId: number) =>
  request("/v2/reports/{aliasId}/links/{linkId}", "delete", {
    path: { aliasId, linkId },
  });
