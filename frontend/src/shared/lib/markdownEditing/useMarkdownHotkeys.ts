import { useCallback, type KeyboardEvent } from "react";
import { isApplePlatform } from "@/shared/lib/keyboard";
import { applyTextEdit } from "./applyTextEdit";
import { resolveMarkdownKey, type SubmitMode } from "./keymap";
import type { TextEdit } from "./types";

const resolveSubmitMode = (
  hasSubmit: boolean,
  submitOnEnter: boolean
): SubmitMode => {
  if (!hasSubmit) return "none";
  return submitOnEnter ? "enter" : "modEnter";
};

type Options = {
  /** Без onSubmit Enter переносит строку, с ним — отправляет. */
  onSubmit?: () => void;
  /** false — отправляет только ⌘Enter, обычный Enter переносит строку. */
  submitOnEnter?: boolean;
  /** ⌘U — выбор файла для вложения. */
  onAttachFile?: () => void;
  /** Предел длины текста: разметка не должна его переполнять. */
  maxLength?: number;
};

/** Правка влезает в лимит: иначе бекенд отклонит текст, выросший на символы разметки. */
const fits = (value: string, edit: TextEdit, maxLength?: number): boolean => {
  if (maxLength === undefined) return true;
  const next = value.length - (edit.to - edit.from) + edit.insert.length;
  return next <= maxLength;
};

export const useMarkdownHotkeys = ({
  onSubmit,
  submitOnEnter = true,
  onAttachFile,
  maxLength,
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
          submit: resolveSubmitMode(Boolean(onSubmit), submitOnEnter),
          canAttach: Boolean(onAttachFile),
          isApple: isApplePlatform(),
        }
      );
      if (!action) return;

      event.preventDefault();
      if (action.type === "submit") onSubmit?.();
      if (action.type === "attach") onAttachFile?.();
      if (
        action.type === "edit" &&
        fits(textarea.value, action.edit, maxLength)
      ) {
        applyTextEdit(textarea, action.edit);
      }
    },
    [onSubmit, submitOnEnter, onAttachFile, maxLength]
  );
