import { useEffect, useRef } from "react";
import { isEditableTarget, matchesHotkey, type Hotkey } from "./hotkey";

type Options = {
  enabled?: boolean;
  /** Срабатывать и при фокусе в поле ввода. По умолчанию — только вне полей. */
  inEditable?: boolean;
};

/** Глобальное сочетание на window. Событие, уже обработанное полем, пропускается. */
export const useHotkey = (
  hotkeys: Hotkey | Hotkey[],
  handler: () => void,
  { enabled = true, inEditable = false }: Options = {}
) => {
  const handlerRef = useRef(handler);
  handlerRef.current = handler;
  const list = Array.isArray(hotkeys) ? hotkeys : [hotkeys];
  const key = JSON.stringify(list);

  useEffect(() => {
    if (!enabled) return;
    const parsed: Hotkey[] = JSON.parse(key);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.isComposing) return;
      if (!inEditable && isEditableTarget(event.target)) return;
      if (!parsed.some((hotkey) => matchesHotkey(event, hotkey))) return;
      event.preventDefault();
      handlerRef.current();
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [enabled, inEditable, key]);
};
