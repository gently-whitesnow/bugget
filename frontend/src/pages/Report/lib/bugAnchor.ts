import { createNestedEntityAnchor } from "./nestedEntityAnchor";

const bugAnchor = createNestedEntityAnchor("bug", { includeParentId: false });

/**
 * Формирует id элемента для бага (используется для якорных ссылок).
 */
export const getBugElementId = (bugId: number): string =>
  bugAnchor.getElementId(bugId);

/**
 * Формирует href для ссылки на баг.
 */
export const getBugAnchorHref = (bugId: number): string =>
  bugAnchor.getAnchorHref(bugId);

/**
 * Паттерн для извлечения id бага из хэша URL.
 * Используется в useScrollToHash.
 */
export const bugHashPattern = bugAnchor.hashPattern;
