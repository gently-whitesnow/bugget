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
