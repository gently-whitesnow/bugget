export function insertPlainTextAtRange({
  range,
  selection,
  clipboardText,
}: {
  range: Range;
  selection: Selection;
  clipboardText: string;
}) {
  // Deprecated in spec but no modern replacement exists for undo-compatible
  // contentEditable insertion. All major browsers still support it.
  if (!document.execCommand("insertText", false, clipboardText)) {
    range.deleteContents();
    range.insertNode(document.createTextNode(clipboardText));
    range.collapse(false);
    selection.removeAllRanges();
    selection.addRange(range);
  }
}
