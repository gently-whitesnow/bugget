import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Комментарии ───────────────────────────────────────────────────────────── */

export type CommentBody = Body<
  "/v2/reports/{aliasId}/bugs/{bugId}/comments",
  "post"
>;
export type CommentResult = Result<
  "/v2/reports/{aliasId}/bugs/{bugId}/comments",
  "post"
>;

export const createComment = (
  aliasId: string,
  bugId: number,
  body: CommentBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/comments", "post", {
    path: { aliasId, bugId },
    body,
  });

export const createCommentWithAttachments = (
  aliasId: string,
  bugId: number,
  text: string,
  files: File[],
  audience?: NonNullable<CommentBody["audience"]>
) =>
  request(
    "/v2/reports/{aliasId}/bugs/{bugId}/comments/with-attachments",
    "post",
    { path: { aliasId, bugId }, multipart: { text, audience, files } }
  );

export const updateComment = (
  aliasId: string,
  bugId: number,
  commentId: number,
  body: CommentBody
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}", "put", {
    path: { aliasId, bugId, commentId },
    body,
  });

export const deleteComment = (
  aliasId: string,
  bugId: number,
  commentId: number
) =>
  request("/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}", "delete", {
    path: { aliasId, bugId, commentId },
  });
