import { useCallback, type KeyboardEvent } from "react";
import { isApplePlatform } from "../hotkey";
import { applyTextEdit } from "./applyTextEdit";
import { resolveMarkdownKey } from "./keymap";

type Options = {
  /** Без onSubmit Enter переносит строку, с ним — отправляет. */
  onSubmit?: () => void;
};

export const useMarkdownHotkeys = ({ onSubmit }: Options = {}) =>
  useCallback(
    (event: KeyboardEvent<HTMLTextAreaElement>) => {
      if (event.defaultPrevented || event.nativeEvent.isComposing) return;

      const textarea = event.currentTarget;
      const action = resolveMarkdownKey(
        event,
        {
          value: textarea.value,
          start: textarea.selectionStart,
          end: textarea.selectionEnd,
        },
        { canSubmit: Boolean(onSubmit), isApple: isApplePlatform() }
      );
      if (!action) return;

      event.preventDefault();
      if (action.type === "submit") onSubmit?.();
      if (action.type === "edit") applyTextEdit(textarea, action.edit);
    },
    [onSubmit]
  );
