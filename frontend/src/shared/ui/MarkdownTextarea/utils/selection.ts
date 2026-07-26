/**
 * Утилиты для работы с selection и курсором в contentEditable элементах
 */

/**
 * Сохраняет текущую позицию курсора
 */
export const saveSelection = (): Range | null => {
  const selection = window.getSelection();
  if (selection && selection.rangeCount > 0) {
    return selection.getRangeAt(0).cloneRange();
  }
  return null;
};

/**
 * Восстанавливает позицию курсора
 */
export const restoreSelection = (range: Range | null): void => {
  if (!range) return;

  const selection = window.getSelection();
  if (selection) {
    try {
      selection.removeAllRanges();
      selection.addRange(range);
    } catch {
      // Игнорируем ошибки восстановления курсора
    }
  }
};

/**
 * Перемещает курсор в конец элемента
 */
export const moveCursorToEnd = (element: HTMLElement): void => {
  const range = document.createRange();
  const selection = window.getSelection();
  if (selection && element.lastChild) {
    range.selectNodeContents(element);
    range.collapse(false);
    selection.removeAllRanges();
    selection.addRange(range);
  }
};

/**
 * Перемещает курсор после указанного узла
 */
export const moveCursorAfterNode = (node: Node): void => {
  const range = document.createRange();
  const selection = window.getSelection();
  if (selection) {
    range.setStartAfter(node);
    range.collapse(true);
    selection.removeAllRanges();
    selection.addRange(range);
  }
};
