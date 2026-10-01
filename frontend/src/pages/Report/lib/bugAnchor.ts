import { createNestedEntityAnchor } from "./nestedEntityAnchor";

const bugAnchor = createNestedEntityAnchor("bug", { includeParentId: false });

/** Id элемента бага для якорных ссылок. */
export const getBugElementId = (bugId: number): string =>
  bugAnchor.getElementId(bugId);

export const getBugAnchorHref = (bugId: number): string =>
  bugAnchor.getAnchorHref(bugId);

/** Паттерн извлечения id бага из хэша URL (для useScrollToHash). */
export const bugHashPattern = bugAnchor.hashPattern;
