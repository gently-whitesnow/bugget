export type Hotkey = {
  /** KeyboardEvent.code: "KeyB", "Digit1", "Enter". */
  code: string;
  /** ⌘ на Apple, Ctrl на остальных. */
  mod?: boolean;
  shift?: boolean;
  alt?: boolean;
};

type HotkeyEvent = Pick<
  KeyboardEvent,
  "code" | "key" | "metaKey" | "ctrlKey" | "shiftKey" | "altKey"
>;

export const isApplePlatform = (): boolean => {
  if (typeof navigator === "undefined") return false;
  const nav = navigator as Navigator & {
    userAgentData?: { platform?: string };
  };
  const platform = nav.userAgentData?.platform || nav.platform || "";
  return /mac|iphone|ipad|ipod/i.test(platform);
};

// Латинская буква из key переживает Dvorak и Colemak; на ЙЦУКЕН и с ⌥ key не латинский —
// тогда нужна физическая клавиша из code.
const resolveCode = (event: HotkeyEvent): string =>
  /^[a-z]$/i.test(event.key) ? `Key${event.key.toUpperCase()}` : event.code;

export const matchesHotkey = (
  event: HotkeyEvent,
  hotkey: Hotkey,
  isApple: boolean = isApplePlatform()
): boolean => {
  const modPressed = isApple ? event.metaKey : event.ctrlKey;
  const foreignModPressed = isApple ? event.ctrlKey : event.metaKey;

  return (
    !foreignModPressed &&
    modPressed === Boolean(hotkey.mod) &&
    event.shiftKey === Boolean(hotkey.shift) &&
    event.altKey === Boolean(hotkey.alt) &&
    resolveCode(event) === hotkey.code
  );
};

const keyLabels: Record<string, string> = {
  Enter: "Enter",
  Escape: "Esc",
  Period: ".",
  Slash: "/",
  Tab: "Tab",
};

const keyLabel = (code: string): string =>
  keyLabels[code] ?? code.replace(/^(Key|Digit)/, "");

/** Подпись для подсказок: «⌥⇧⌘C» на Apple (порядок из HIG), «Ctrl+Alt+Shift+C» на остальных. */
export const formatHotkey = (
  hotkey: Hotkey,
  isApple: boolean = isApplePlatform()
): string => {
  const key = keyLabel(hotkey.code);
  if (isApple) {
    return `${hotkey.alt ? "⌥" : ""}${hotkey.shift ? "⇧" : ""}${hotkey.mod ? "⌘" : ""}${key}`;
  }
  return [
    hotkey.mod && "Ctrl",
    hotkey.alt && "Alt",
    hotkey.shift && "Shift",
    key,
  ]
    .filter(Boolean)
    .join("+");
};

/** Значение для aria-keyshortcuts: «Meta+K» / «Control+K». */
export const ariaHotkey = (
  hotkey: Hotkey,
  isApple: boolean = isApplePlatform()
): string =>
  [
    hotkey.mod && (isApple ? "Meta" : "Control"),
    hotkey.alt && "Alt",
    hotkey.shift && "Shift",
    keyLabel(hotkey.code),
  ]
    .filter(Boolean)
    .join("+");

/** Фокус в поле ввода: одиночные клавиши вроде «/» там — обычный текст. */
export const isEditableTarget = (target: EventTarget | null): boolean =>
  target instanceof HTMLElement &&
  (target.isContentEditable ||
    /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
