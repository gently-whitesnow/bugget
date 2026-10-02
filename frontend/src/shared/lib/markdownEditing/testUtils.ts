import type { TextEdit, TextSnapshot } from "./types";

/** "a {bc} d" — выделение, "a | d" — курсор. */
export const snap = (marked: string): TextSnapshot => {
  const cursor = marked.indexOf("|");
  if (cursor !== -1) {
    return { value: marked.replace("|", ""), start: cursor, end: cursor };
  }
  const start = marked.indexOf("{");
  const end = marked.indexOf("}") - 1;
  return { value: marked.replace("{", "").replace("}", ""), start, end };
};

export const render = (
  { value }: TextSnapshot,
  edit: TextEdit | null
): string => {
  if (!edit) return "<no edit>";
  const next = value.slice(0, edit.from) + edit.insert + value.slice(edit.to);
  const { selectionStart: s, selectionEnd: e } = edit;
  if (s === e) return `${next.slice(0, s)}|${next.slice(s)}`;
  return `${next.slice(0, s)}{${next.slice(s, e)}}${next.slice(e)}`;
};

export const run = (
  marked: string,
  command: (snapshot: TextSnapshot) => TextEdit | null
): string => {
  const snapshot = snap(marked);
  return render(snapshot, command(snapshot));
};
