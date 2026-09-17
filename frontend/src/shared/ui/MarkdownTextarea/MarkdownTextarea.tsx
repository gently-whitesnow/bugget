import {
  forwardRef,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  type ChangeEvent,
  type ClipboardEvent,
  type CSSProperties,
  type KeyboardEvent,
} from "react";
import {
  applyTextEdit,
  linkFromPaste,
  useMarkdownHotkeys,
} from "@/shared/lib/keyboard";

type Props = {
  value: string;
  placeholder?: string;
  autoFocus?: boolean;
  maxLength?: number;
  className?: string;
  style?: CSSProperties;
  rows?: number;
  /** Вставка URL поверх выделения делает из него ссылку. */
  enableLinkInsertion?: boolean;
  onBlur?: (value: string) => void;
  onInput?: (value: string) => void;
  onFocus?: () => void;
  onPaste?: (event: ClipboardEvent<HTMLTextAreaElement>) => void;
  onKeyDown?: (event: KeyboardEvent<HTMLTextAreaElement>) => void;
  /** Enter и ⌘Enter. Без него Enter переносит строку. */
  onSubmit?: (value: string) => void;
  /** Esc. Без него поле просто теряет фокус. */
  onCancel?: () => void;
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
      enableLinkInsertion = true,
      onBlur,
      onInput,
      onFocus,
      onPaste,
      onKeyDown,
      onSubmit,
      onCancel,
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

    const baseMinHeight = `${rows * 2.5}rem`;

    // Растим min-height, а не height: владелец может растянуть поле выше текста (self-stretch).
    const adjustHeight = useCallback(() => {
      const textarea = textareaRef.current;
      if (!textarea) return;
      textarea.style.height = "auto";
      textarea.style.minHeight = baseMinHeight;
      // scrollHeight не включает бордер: без поправки последняя строка обрезается.
      const border = textarea.offsetHeight - textarea.clientHeight;
      textarea.style.minHeight = `max(${baseMinHeight}, ${textarea.scrollHeight + border}px)`;
      textarea.style.height = "";
    }, [baseMinHeight]);

    useLayoutEffect(() => {
      const textarea = textareaRef.current;
      if (textarea && value !== lastEmittedRef.current) {
        textarea.value = value;
        lastEmittedRef.current = value;
      }
      adjustHeight();
    }, [value, adjustHeight]);

    useEffect(() => {
      const textarea = textareaRef.current;
      if (!autoFocus || !textarea) return;
      textarea.focus();
      textarea.setSelectionRange(textarea.value.length, textarea.value.length);
    }, [autoFocus]);

    const handleSubmit = useCallback(() => {
      onSubmit?.(textareaRef.current?.value ?? "");
    }, [onSubmit]);

    const handleMarkdownKeys = useMarkdownHotkeys({
      onSubmit: onSubmit ? handleSubmit : undefined,
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
      if (!edit) return;

      event.preventDefault();
      applyTextEdit(textarea, edit);
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
