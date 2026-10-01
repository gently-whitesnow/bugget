import type { TextEdit } from "./types";

/**
 * setRangeText лимит поля не соблюдает, поэтому длину проверяем сами. Экспортируется и
 * отдельно: решение «перехватывать вставку или отдать её браузеру» нужно до preventDefault.
 */
export const fitsMaxLength = (
  value: string,
  edit: TextEdit,
  maxLength?: number
): boolean => {
  if (maxLength === undefined) return true;
  return value.length - (edit.to - edit.from) + edit.insert.length <= maxLength;
};

// execCommand устарел, но только он кладёт программную правку в родной стек undo поля.
const insertWithUndo = ({ from, to, insert }: TextEdit): boolean => {
  if (typeof document.execCommand !== "function") return false;
  try {
    if (insert) return document.execCommand("insertText", false, insert);
    return from === to || document.execCommand("delete");
  } catch {
    return false;
  }
};

/** false — правка не влезает в лимит и не применена: текст лучше оставить как был. */
export const applyTextEdit = (
  textarea: HTMLTextAreaElement,
  edit: TextEdit,
  maxLength?: number
): boolean => {
  if (!fitsMaxLength(textarea.value, edit, maxLength)) return false;

  textarea.focus();
  textarea.setSelectionRange(edit.from, edit.to);

  if (!insertWithUndo(edit)) {
    textarea.setRangeText(edit.insert, edit.from, edit.to, "end");
    // React узнаёт о смене value только по событию input.
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
  }

  textarea.setSelectionRange(edit.selectionStart, edit.selectionEnd);
  return true;
};
