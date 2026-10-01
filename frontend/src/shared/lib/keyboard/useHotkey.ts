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
  // Слушатель вешаем один раз и читаем свежие значения из ref: иначе пришлось бы
  // сравнивать массив сочетаний в зависимостях эффекта.
  const handlerRef = useRef(handler);
  handlerRef.current = handler;
  const hotkeysRef = useRef(hotkeys);
  hotkeysRef.current = hotkeys;

  useEffect(() => {
    if (!enabled) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.isComposing) return;
      if (!inEditable && isEditableTarget(event.target)) return;
      const current = hotkeysRef.current;
      const list = Array.isArray(current) ? current : [current];
      if (!list.some((hotkey) => matchesHotkey(event, hotkey))) return;
      event.preventDefault();
      handlerRef.current();
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [enabled, inEditable]);
};
