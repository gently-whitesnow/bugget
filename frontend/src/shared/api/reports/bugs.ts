import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Баги ──────────────────────────────────────────────────────────────────── */

export type CreateBugBody = Body<"/v2/reports/{aliasId}/bugs", "post">;
export type CreateBugResult = Result<"/v2/reports/{aliasId}/bugs", "post">;

export const createBug = (aliasId: string, body: CreateBugBody) =>
  request("/v2/reports/{aliasId}/bugs", "post", { path: { aliasId }, body });

export type PatchBugBody = Body<"/v2/reports/{aliasId}/bugs/{bugId}", "patch">;
export type PatchBugResult = Result<
  "/v2/reports/{aliasId}/bugs/{bugId}",
  "patch"
>;

export const patchBug = (aliasId: string, bugId: number, body: PatchBugBody) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}", "patch", {
    path: { aliasId, bugId },
    body,
  });
