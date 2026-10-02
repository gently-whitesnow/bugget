import {
  forwardRef,
  useCallback,
  useLayoutEffect,
  useRef,
  type ChangeEvent,
  type ClipboardEvent,
  type CSSProperties,
  type KeyboardEvent,
} from "react";
import { useAutoHeight } from "@/shared/lib/hooks";
import {
  applyTextEdit,
  fitsMaxLength,
  linkFromPaste,
  useMarkdownHotkeys,
} from "@/shared/lib/markdownEditing";

type KeyboardActions = {
  /** ⌘Enter, а с submitOn "enter" — и обычный Enter. */
  onSubmit?: (value: string) => void;
  /** По умолчанию отправляет и Enter; "modEnter" оставляет Enter под перенос строки. */
  submitOn?: "enter" | "modEnter";
  /** Esc. Без него поле просто теряет фокус. */
  onCancel?: () => void;
  /** ⌘U. Без него сочетание достаётся браузеру. */
  onAttachFile?: () => void;
};

type Props = {
  value: string;
  placeholder?: string;
  autoFocus?: boolean;
  maxLength?: number;
  className?: string;
  style?: CSSProperties;
  rows?: number;
  /** Минимальная высота вместо rows: владелец может держать её постоянной между режимами. */
  minHeight?: string;
  /** Вставка URL поверх выделения делает из него ссылку. */
  enableLinkInsertion?: boolean;
  onBlur?: (value: string) => void;
  onInput?: (value: string) => void;
  onFocus?: () => void;
  onPaste?: (event: ClipboardEvent<HTMLTextAreaElement>) => void;
  onKeyDown?: (event: KeyboardEvent<HTMLTextAreaElement>) => void;
  /** Что поле умеет по клавишам: отправка, отмена, вложение. */
  actions?: KeyboardActions;
};

/** Поле с исходным markdown: разметка видна как есть, форматирование — с клавиатуры. */
const MarkdownTextarea = forwardRef<HTMLTextAreaElement, Props>(
  (
    {
      value,
      placeholder = "",
      autoFocus = false,
      maxLength,
      className = "",
      style,
      rows = 1,
      minHeight,
      enableLinkInsertion = true,
      onBlur,
      onInput,
      onFocus,
      onPaste,
      onKeyDown,
      actions = {},
    },
    ref
  ) => {
    const textareaRef = useRef<HTMLTextAreaElement | null>(null);

    const setRefs = useCallback(
      (node: HTMLTextAreaElement | null) => {
        textareaRef.current = node;
        if (typeof ref === "function") ref(node);
        else if (ref) ref.current = node;
      },
      [ref]
    );

    // Поле неконтролируемое: часть владельцев обновляет value только на blur.
    const lastEmittedRef = useRef(value);

    const baseMinHeight = minHeight ?? `${rows * 2.5}rem`;
    const adjustHeight = useAutoHeight(textareaRef, baseMinHeight);

    useLayoutEffect(() => {
      const textarea = textareaRef.current;
      if (textarea && value !== lastEmittedRef.current) {
        textarea.value = value;
        lastEmittedRef.current = value;
      }
      adjustHeight();
    }, [value, adjustHeight]);

    // Именно layout-эффект: фокус после отрисовки даёт кадр без подсветки — обводка мигает.
    useLayoutEffect(() => {
      const textarea = textareaRef.current;
      if (!autoFocus || !textarea) return;
      textarea.focus();
      textarea.setSelectionRange(textarea.value.length, textarea.value.length);
    }, [autoFocus]);

    const { onSubmit, submitOn = "enter", onCancel, onAttachFile } = actions;

    // Значение берём из DOM, а не из пропа: владелец может обновлять его только на blur.
    const handleSubmit = useCallback(() => {
      onSubmit?.(textareaRef.current?.value ?? "");
    }, [onSubmit]);

    const handleMarkdownKeys = useMarkdownHotkeys({
      onSubmit: onSubmit ? handleSubmit : undefined,
      submitOnEnter: submitOn === "enter",
      onAttachFile,
      maxLength,
    });

    const handleChange = (event: ChangeEvent<HTMLTextAreaElement>) => {
      lastEmittedRef.current = event.target.value;
      adjustHeight();
      onInput?.(event.target.value);
    };

    const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
      onKeyDown?.(event);
      if (event.defaultPrevented) return;

      if (event.key === "Escape" && !event.nativeEvent.isComposing) {
        event.preventDefault();
        if (onCancel) onCancel();
        else event.currentTarget.blur();
        return;
      }

      handleMarkdownKeys(event);
    };

    const handlePaste = (event: ClipboardEvent<HTMLTextAreaElement>) => {
      onPaste?.(event);
      if (event.defaultPrevented || !enableLinkInsertion) return;

      const textarea = event.currentTarget;
      const edit = linkFromPaste(
        {
          value: textarea.value,
          start: textarea.selectionStart,
          end: textarea.selectionEnd,
        },
        event.clipboardData.getData("text/plain")
      );
      // Ссылка в лимит не влезла — вставку отдаём браузеру: он обрежет текст сам.
      if (!edit || !fitsMaxLength(textarea.value, edit, maxLength)) return;

      event.preventDefault();
      applyTextEdit(textarea, edit, maxLength);
    };

    return (
      <textarea
        ref={setRefs}
        defaultValue={value}
        rows={rows}
        maxLength={maxLength}
        placeholder={placeholder}
        onChange={handleChange}
        onFocus={onFocus}
        onBlur={(event) => onBlur?.(event.currentTarget.value)}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        className={`resize-none overflow-hidden ${className}`.trim()}
        style={{ minHeight: baseMinHeight, ...style }}
      />
    );
  }
);

MarkdownTextarea.displayName = "MarkdownTextarea";

export default MarkdownTextarea;
