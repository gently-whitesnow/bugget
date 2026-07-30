import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Шаги воспроизведения ──────────────────────────────────────────────────── */

export type BugStepBody = Body<
  "/v2/reports/{aliasId}/bugs/{bugId}/steps",
  "post"
>;
export type BugStepResult = Result<
  "/v2/reports/{aliasId}/bugs/{bugId}/steps",
  "post"
>;

export const createBugStep = (
  aliasId: string,
  bugId: number,
  body: BugStepBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/steps", "post", {
    path: { aliasId, bugId },
    body,
  });

export const patchBugStep = (
  aliasId: string,
  bugId: number,
  stepId: number,
  body: BugStepBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}", "patch", {
    path: { aliasId, bugId, stepId },
    body,
  });

export const deleteBugStep = (aliasId: string, bugId: number, stepId: number) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}", "delete", {
    path: { aliasId, bugId, stepId },
  });

export type BugStepsOrderBody = Body<
  "/v2/reports/{aliasId}/bugs/{bugId}/steps/order",
  "put"
>;
export type BugStepsOrderResult = Result<
  "/v2/reports/{aliasId}/bugs/{bugId}/steps/order",
  "put"
>;

export const updateBugStepsOrder = (
  aliasId: string,
  bugId: number,
  body: BugStepsOrderBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/steps/order", "put", {
    path: { aliasId, bugId },
    body,
  });
