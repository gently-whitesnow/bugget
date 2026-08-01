import { request } from "./client";
import type { Body, Query, Result } from "./client";

/** Значение `attachType` — из контракта операции, а не число рядом с вызовом. */
type AttachType = NonNullable<
  Query<"/v2/reports/{aliasId}/bugs/{bugId}/attachments", "post">
>["attachType"];

/* ── Вложения ──────────────────────────────────────────────────────────────── */

export type AttachmentResult = Result<
  "/v2/reports/{aliasId}/bugs/{bugId}/attachments",
  "post"
>;

export type AttachmentRenameBody = Body<
  "/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}",
  "patch"
>;

export const createBugAttachment = (
  aliasId: string,
  bugId: number,
  attachType: AttachType,
  file: File
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/attachments", "post", {
    path: { aliasId, bugId },
    query: { attachType },
    multipart: { file },
  });

export const deleteBugAttachment = (
  aliasId: string,
  bugId: number,
  id: number
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}", "delete", {
    path: { aliasId, bugId, id },
  });

export const renameBugAttachment = (
  aliasId: string,
  bugId: number,
  id: number,
  body: AttachmentRenameBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}", "patch", {
    path: { aliasId, bugId, id },
    body,
  });

export const createCommentAttachment = (
  aliasId: string,
  bugId: number,
  commentId: number,
  file: File
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments",
    "post",
    { path: { aliasId, bugId, commentId }, multipart: { file } }
  );

export const deleteCommentAttachment = (
  aliasId: string,
  bugId: number,
  commentId: number,
  id: number
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}",
    "delete",
    { path: { aliasId, bugId, commentId, id } }
  );

export const renameCommentAttachment = (
  aliasId: string,
  bugId: number,
  commentId: number,
  id: number,
  body: AttachmentRenameBody
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}",
    "patch",
    { path: { aliasId, bugId, commentId, id }, body }
  );

export const createBugStepAttachment = (
  aliasId: string,
  bugId: number,
  stepId: number,
  file: File
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments",
    "post",
    { path: { aliasId, bugId, stepId }, multipart: { file } }
  );

export const deleteBugStepAttachment = (
  aliasId: string,
  bugId: number,
  stepId: number,
  id: number
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}",
    "delete",
    { path: { aliasId, bugId, stepId, id } }
  );

export const renameBugStepAttachment = (
  aliasId: string,
  bugId: number,
  stepId: number,
  id: number,
  body: AttachmentRenameBody
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}",
    "patch",
    { path: { aliasId, bugId, stepId, id }, body }
  );
