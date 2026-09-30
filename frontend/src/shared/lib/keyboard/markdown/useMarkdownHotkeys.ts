import { useCallback, type KeyboardEvent } from "react";
import { isApplePlatform } from "../hotkey";
import { applyTextEdit } from "./applyTextEdit";
import { resolveMarkdownKey } from "./keymap";

type Options = {
  /** Без onSubmit Enter переносит строку, с ним — отправляет. */
  onSubmit?: () => void;
  /** false — отправляет только ⌘Enter, обычный Enter переносит строку. */
  submitOnEnter?: boolean;
  /** ⌘U — выбор файла для вложения. */
  onAttachFile?: () => void;
};

export const useMarkdownHotkeys = ({
  onSubmit,
  submitOnEnter = true,
  onAttachFile,
}: Options = {}) =>
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
        {
          canSubmit: Boolean(onSubmit),
          submitOnEnter,
          canAttach: Boolean(onAttachFile),
          isApple: isApplePlatform(),
        }
      );
      if (!action) return;

      event.preventDefault();
      if (action.type === "submit") onSubmit?.();
      if (action.type === "attach") onAttachFile?.();
      if (action.type === "edit") applyTextEdit(textarea, action.edit);
    },
    [onSubmit, submitOnEnter, onAttachFile]
  );
