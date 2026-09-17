import { matchesHotkey, type Hotkey } from "../hotkey";
import { toggleCodeBlock, toggleLineFormat, toggleQuote } from "./blocks";
import { toggleInlineFormat, toggleLink } from "./inline";
import { continueList, isInListItem, shiftIndent } from "./lists";
import type { TextEdit, TextSnapshot } from "./types";

export type MarkdownCommand = {
  id: string;
  hotkey: Hotkey;
  run: (snapshot: TextSnapshot) => TextEdit | null;
};

export const markdownCommands: MarkdownCommand[] = [
  {
    id: "bold",
    hotkey: { code: "KeyB", mod: true },
    run: (s) => toggleInlineFormat(s, "bold"),
  },
  {
    id: "italic",
    hotkey: { code: "KeyI", mod: true },
    run: (s) => toggleInlineFormat(s, "italic"),
  },
  {
    id: "strikethrough",
    hotkey: { code: "KeyX", mod: true, shift: true },
    run: (s) => toggleInlineFormat(s, "strikethrough"),
  },
  {
    id: "code",
    hotkey: { code: "KeyE", mod: true },
    run: (s) => toggleInlineFormat(s, "code"),
  },
  {
    // ⌘⇧C и ⌘⌥C в Chrome заняты DevTools, страница их не перехватит.
    id: "codeBlock",
    hotkey: { code: "KeyC", mod: true, alt: true, shift: true },
    run: toggleCodeBlock,
  },
  { id: "link", hotkey: { code: "KeyK", mod: true }, run: toggleLink },
  ...([1, 2, 3] as const).map((level) => ({
    id: `heading${level}`,
    hotkey: { code: `Digit${level}`, mod: true, alt: true },
    run: (s: TextSnapshot) => toggleLineFormat(s, { kind: "heading", level }),
  })),
  {
    id: "bulletList",
    hotkey: { code: "Digit8", mod: true, shift: true },
    run: (s) => toggleLineFormat(s, { kind: "bulletList" }),
  },
  {
    id: "orderedList",
    hotkey: { code: "Digit7", mod: true, shift: true },
    run: (s) => toggleLineFormat(s, { kind: "orderedList" }),
  },
  {
    id: "quote",
    hotkey: { code: "Digit9", mod: true, shift: true },
    run: toggleQuote,
  },
];

export type MarkdownKeyAction =
  | { type: "edit"; edit: TextEdit }
  | { type: "submit" }
  /** Сочетание наше, но править нечего: браузеру его всё равно не отдаём. */
  | { type: "consume" };

type KeyEvent = Pick<
  KeyboardEvent,
  "key" | "code" | "metaKey" | "ctrlKey" | "shiftKey" | "altKey"
>;

type Options = {
  /** Есть отправка: Enter и ⌘Enter отправляют, перенос — Shift/Option+Enter. */
  canSubmit: boolean;
  isApple: boolean;
};

const newline = (snapshot: TextSnapshot): TextEdit => {
  const edit = continueList(snapshot);
  if (edit) return edit;
  const cursor = snapshot.start + 1;
  return {
    from: snapshot.start,
    to: snapshot.end,
    insert: "\n",
    selectionStart: cursor,
    selectionEnd: cursor,
  };
};

export const resolveMarkdownKey = (
  event: KeyEvent,
  snapshot: TextSnapshot,
  { canSubmit, isApple }: Options
): MarkdownKeyAction | null => {
  const mod = isApple ? event.metaKey : event.ctrlKey;
  const foreignMod = isApple ? event.ctrlKey : event.metaKey;

  if (event.key === "Enter" && !foreignMod) {
    const lineBreak = event.shiftKey !== event.altKey;
    if (mod) {
      return canSubmit && !event.shiftKey && !event.altKey
        ? { type: "submit" }
        : null;
    }
    if (lineBreak) return { type: "edit", edit: newline(snapshot) };
    if (event.shiftKey) return null;
    return canSubmit
      ? { type: "submit" }
      : { type: "edit", edit: newline(snapshot) };
  }

  if (event.key === "Tab" && !mod && !foreignMod && !event.altKey) {
    const hasSelection = snapshot.start !== snapshot.end;
    // Без выделения и вне списка Tab уводит фокус дальше — иначе из поля не выйти с клавиатуры.
    if (!hasSelection && !isInListItem(snapshot)) return null;
    const edit = shiftIndent(snapshot, event.shiftKey ? "out" : "in");
    return edit ? { type: "edit", edit } : { type: "consume" };
  }

  const command = markdownCommands.find((c) =>
    matchesHotkey(event, c.hotkey, isApple)
  );
  if (!command) return null;
  const edit = command.run(snapshot);
  return edit ? { type: "edit", edit } : { type: "consume" };
};
