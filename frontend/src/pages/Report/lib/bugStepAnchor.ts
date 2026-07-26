import { createNestedEntityAnchor } from "./nestedEntityAnchor";

const bugStepAnchor = createNestedEntityAnchor("step");

/**
 * Формирует id элемента для шага воспроизведения (используется для якорных ссылок).
 */
export const getBugStepElementId = (bugId: number, stepId: number): string =>
  bugStepAnchor.getElementId(bugId, stepId);

/**
 * Формирует href для ссылки на шаг воспроизведения.
 */
export const getBugStepAnchorHref = (bugId: number, stepId: number): string =>
  bugStepAnchor.getAnchorHref(bugId, stepId);

/**
 * Паттерн для извлечения bugId/stepId из хэша URL.
 */
export const bugStepHashPattern = bugStepAnchor.hashPattern;
