import { createNestedEntityAnchor } from "./nestedEntityAnchor";

const commentAnchor = createNestedEntityAnchor("comment");

/**
 * Формирует id элемента для комментария (используется для якорных ссылок).
 */
export const getCommentElementId = (bugId: number, commentId: number): string =>
  commentAnchor.getElementId(bugId, commentId);

/**
 * Формирует href для ссылки на комментарий.
 */
export const getCommentAnchorHref = (
  bugId: number,
  commentId: number
): string => commentAnchor.getAnchorHref(bugId, commentId);

/**
 * Паттерн для извлечения bugId/commentId из хэша URL.
 */
export const commentHashPattern = commentAnchor.hashPattern;
