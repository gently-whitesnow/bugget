import { appHotkeys } from "@/shared/lib/keyboard";
import { matchesHotkey, type Hotkey } from "@/shared/lib/keyboard";
import { toggleCodeBlock, toggleLineFormat, toggleQuote } from "./blocks";
import { toggleInlineFormat, toggleLink } from "./inline";
import { continueList, isInListItem, shiftIndent } from "./lists";
import type { TextEdit, TextSnapshot } from "./types";

export type MarkdownCommand = {
  id: string;
  label: string;
  /** Равноправные сочетания: основное может быть занято браузером. */
  hotkeys: Hotkey[];
  run: (snapshot: TextSnapshot) => TextEdit | null;
};

export const markdownCommands: MarkdownCommand[] = [
  {
    id: "bold",
    label: "Жирный",
    hotkeys: [{ code: "KeyB", mod: true }],
    run: (s) => toggleInlineFormat(s, "bold"),
  },
  {
    id: "italic",
    label: "Курсив",
    hotkeys: [{ code: "KeyI", mod: true }],
    run: (s) => toggleInlineFormat(s, "italic"),
  },
  {
    id: "strikethrough",
    label: "Зачёркнутый",
    hotkeys: [{ code: "KeyX", mod: true, shift: true }],
    run: (s) => toggleInlineFormat(s, "strikethrough"),
  },
  {
    id: "code",
    label: "Код в строке",
    // ⌘E в Chrome на Mac — «искать выделенное», у части пользователей перехвачен.
    hotkeys: [
      { code: "KeyE", mod: true },
      { code: "KeyE", mod: true, shift: true },
    ],
    run: (s) => toggleInlineFormat(s, "code"),
  },
  {
    // ⌘⇧C и ⌘⌥C в Chrome заняты DevTools, страница их не перехватит.
    id: "codeBlock",
    label: "Блок кода",
    hotkeys: [{ code: "KeyC", mod: true, alt: true, shift: true }],
    run: toggleCodeBlock,
  },
  {
    id: "link",
    label: "Ссылка",
    hotkeys: [{ code: "KeyK", mod: true }],
    run: toggleLink,
  },
  ...([1, 2, 3] as const).map((level) => ({
    id: `heading${level}`,
    label: `Заголовок ${level}`,
    hotkeys: [{ code: `Digit${level}`, mod: true, alt: true }],
    run: (s: TextSnapshot) => toggleLineFormat(s, { kind: "heading", level }),
  })),
  {
    id: "bulletList",
    label: "Маркированный список",
    hotkeys: [{ code: "Digit8", mod: true, shift: true }],
    run: (s) => toggleLineFormat(s, { kind: "bulletList" }),
  },
  {
    id: "orderedList",
    label: "Нумерованный список",
    hotkeys: [{ code: "Digit7", mod: true, shift: true }],
    run: (s) => toggleLineFormat(s, { kind: "orderedList" }),
  },
  {
    id: "quote",
    label: "Цитата",
    hotkeys: [
      { code: "Digit9", mod: true, shift: true },
      { code: "Period", mod: true, shift: true },
    ],
    run: toggleQuote,
  },
];

export type MarkdownKeyAction =
  | { type: "edit"; edit: TextEdit }
  | { type: "submit" }
  | { type: "attach" }
  /** Сочетание наше, но править нечего: событие всё равно не отдаём браузеру. */
  | { type: "preventDefault" };

/** Чем поле отвечает на Enter: отправкой, отправкой только с ⌘ или переносом строки. */
export type SubmitMode = "enter" | "modEnter" | "none";

type KeyEvent = Pick<
  KeyboardEvent,
  "key" | "code" | "metaKey" | "ctrlKey" | "shiftKey" | "altKey"
>;

type Options = {
  submit: SubmitMode;
  canAttach?: boolean;
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
  { submit, canAttach = false, isApple }: Options
): MarkdownKeyAction | null => {
  const mod = isApple ? event.metaKey : event.ctrlKey;
  const foreignMod = isApple ? event.ctrlKey : event.metaKey;

  if (event.key === "Enter" && !foreignMod) {
    const lineBreak = event.shiftKey !== event.altKey;
    if (mod) {
      return submit !== "none" && !event.shiftKey && !event.altKey
        ? { type: "submit" }
        : null;
    }
    if (lineBreak) return { type: "edit", edit: newline(snapshot) };
    if (event.shiftKey) return null;
    return submit === "enter"
      ? { type: "submit" }
      : { type: "edit", edit: newline(snapshot) };
  }

  if (event.key === "Tab" && !mod && !foreignMod && !event.altKey) {
    // Tab уводит фокус, когда вложенность менять некуда: иначе появляется третье
    // состояние «нажал, ничего не было».
    if (!isInListItem(snapshot)) return null;
    const edit = shiftIndent(snapshot, event.shiftKey ? "out" : "in");
    return edit ? { type: "edit", edit } : null;
  }

  if (canAttach && matchesHotkey(event, appHotkeys.attachFile, isApple)) {
    return { type: "attach" };
  }

  const command = markdownCommands.find((c) =>
    c.hotkeys.some((hotkey) => matchesHotkey(event, hotkey, isApple))
  );
  if (!command) return null;
  const edit = command.run(snapshot);
  return edit ? { type: "edit", edit } : { type: "preventDefault" };
};
