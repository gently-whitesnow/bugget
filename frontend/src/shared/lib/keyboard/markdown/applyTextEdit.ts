import type { TextEdit } from "./types";

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

export const applyTextEdit = (
  textarea: HTMLTextAreaElement,
  edit: TextEdit
): void => {
  textarea.focus();
  textarea.setSelectionRange(edit.from, edit.to);

  if (!insertWithUndo(edit)) {
    textarea.setRangeText(edit.insert, edit.from, edit.to, "end");
    // React узнаёт о смене value только по событию input.
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
  }

  textarea.setSelectionRange(edit.selectionStart, edit.selectionEnd);
};
