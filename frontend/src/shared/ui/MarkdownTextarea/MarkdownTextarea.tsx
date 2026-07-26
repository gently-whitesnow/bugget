import {
  useRef,
  useEffect,
  KeyboardEvent,
  forwardRef,
  useCallback,
} from "react";
import DOMPurify from "dompurify";
import { markdownToHtml, htmlToMarkdown } from "@/shared/lib/markdown";
import { useLinkPreview } from "@/shared/lib/hooks";
import LinkPreview from "@/shared/ui/LinkPreview";
import {
  saveSelection,
  restoreSelection,
  moveCursorToEnd,
  moveCursorAfterNode,
  validateAndNormalizeUrl,
  createLinkElement,
  getMarkdownLinksOnBlur,
  INDENT,
  indentFragmentLines,
  outdentFragmentLines,
} from "./utils";
import { insertPlainTextAtRange } from "./utils/insertPlaintextAtRange";

const sanitizeEditorHtml = (html: string): string =>
  DOMPurify.sanitize(html, {
    ALLOWED_TAGS: ["a", "br"],
    ALLOWED_ATTR: ["href", "target", "rel", "class", "data-link-url"],
    ALLOW_DATA_ATTR: false,
  });

type Props = {
  value: string;
  placeholder?: string;
  autoFocus?: boolean;
  maxLength?: number;
  className?: string;
  style?: React.CSSProperties;
  rows?: number;
  enableLinkInsertion?: boolean;
  onBlur?: (value: string) => void;
  onInput?: (value: string) => void;
  onPaste?: (event: React.ClipboardEvent<HTMLDivElement>) => void;
  onKeyDown?: (event: KeyboardEvent<HTMLDivElement>) => void;
};

const MarkdownTextarea = forwardRef<HTMLDivElement, Props>(
  (
    {
      value,
      placeholder = "",
      autoFocus = false,
      maxLength,
      className = "",
      style,
      rows = 1,
      onBlur,
      onInput,
      onPaste,
      onKeyDown,
      enableLinkInsertion = true,
    },
    ref
  ) => {
    const internalRef = useRef<HTMLDivElement>(null);
    const shouldInsertLinkRef = useRef(false);
    const lastEmittedMarkdownRef = useRef(value);
    const { linkPreview, closeLinkPreview } = useLinkPreview(internalRef);
    const sanitizeMarkdownToHtml = useCallback(
      (markdown: string) => sanitizeEditorHtml(markdownToHtml(markdown)),
      []
    );

    // Объединяем внутренний ref и внешний ref для передачи на div элемент
    const setRefs = useCallback(
      (node: HTMLDivElement | null) => {
        internalRef.current = node;
        if (ref) {
          if (typeof ref === "function") {
            ref(node);
          } else {
            ref.current = node;
          }
        }
      },
      [ref]
    );

    // Инициализация содержимого при монтировании
    useEffect(() => {
      if (internalRef.current && !internalRef.current.innerHTML && value) {
        internalRef.current.innerHTML = sanitizeMarkdownToHtml(value);
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
      if (autoFocus && internalRef.current) {
        internalRef.current.focus();
        moveCursorToEnd(internalRef.current);
      }
    }, [autoFocus]);

    useEffect(() => {
      // Обновляем HTML содержимое только если значение изменилось извне
      if (internalRef.current && value !== lastEmittedMarkdownRef.current) {
        const wasFocused = document.activeElement === internalRef.current;
        const savedRange = wasFocused ? saveSelection() : null;

        internalRef.current.innerHTML = sanitizeMarkdownToHtml(value);
        lastEmittedMarkdownRef.current = value;

        // Восстанавливаем курсор
        if (wasFocused && savedRange) {
          restoreSelection(savedRange);
        }
      }
    }, [value, sanitizeMarkdownToHtml]);

    const handleBlur = useCallback(() => {
      if (!internalRef.current) return;

      // Обрабатываем markdown-ссылки при потере фокуса
      const processedHtml = getMarkdownLinksOnBlur(internalRef.current);
      internalRef.current.innerHTML = sanitizeEditorHtml(processedHtml);

      if (onBlur) {
        // Получаем markdown из обработанного HTML для передачи в onBlur
        const markdown = htmlToMarkdown(internalRef.current);
        lastEmittedMarkdownRef.current = markdown;
        onBlur(markdown);
      }
    }, [onBlur]);

    const handleInput = useCallback(() => {
      if (!internalRef.current) return;

      let markdown = htmlToMarkdown(internalRef.current);
      // Валидация maxLength для contenteditable
      if (maxLength && markdown.length > maxLength) {
        const trimmed = markdown.substring(0, maxLength);
        internalRef.current.innerHTML = sanitizeMarkdownToHtml(trimmed);
        // Восстанавливаем курсор в конец
        moveCursorToEnd(internalRef.current);
        markdown = trimmed;
      }
      lastEmittedMarkdownRef.current = markdown;
      // Вызываем внешний обработчик
      if (onInput) {
        onInput(markdown);
      }
    }, [maxLength, onInput, sanitizeMarkdownToHtml]);

    const handleDeleteLink = useCallback(() => {
      if (!linkPreview || !internalRef.current) return;

      const linkElement = linkPreview.linkElement;
      const linkText = linkElement.textContent || "";

      // Заменяем ссылку на обычный текст
      const textNode = document.createTextNode(linkText);
      linkElement.parentNode?.replaceChild(textNode, linkElement);

      // Вызываем handleInput для обновления состояния
      handleInput();

      // Сохраняем изменения
      requestAnimationFrame(() => {
        if (internalRef.current && onBlur) {
          const markdown = htmlToMarkdown(internalRef.current);
          lastEmittedMarkdownRef.current = markdown;
          onBlur(markdown);
        }
      });
    }, [linkPreview, handleInput, onBlur]);

    const handleKeyDown = useCallback(
      (event: KeyboardEvent<HTMLDivElement>) => {
        // Вызываем внешний обработчик, если он есть
        if (onKeyDown) {
          onKeyDown(event);
        }

        if (event.key === "Escape") {
          event.preventDefault();
          if (internalRef.current) {
            internalRef.current.innerHTML = sanitizeMarkdownToHtml(value || "");
          }
          lastEmittedMarkdownRef.current = value || "";
          internalRef.current?.blur();
          return;
        }

        // Tab: если есть выделение — indent/outdent, иначе отдаём браузеру (фокус дальше по UI)
        if (event.key === "Tab") {
          const el = internalRef.current;
          const sel = window.getSelection();
          if (!el || !sel || sel.rangeCount === 0) return;

          const range = sel.getRangeAt(0);

          // Если курсор (нет выделения) — не блокируем Tab
          if (range.collapsed) return;

          // Если выделение не внутри нашего редактора — тоже не лезем
          const ca = range.commonAncestorContainer;
          if (
            !el.contains(ca.nodeType === Node.ELEMENT_NODE ? ca : ca.parentNode)
          )
            return;

          event.preventDefault();

          const frag = range.extractContents();

          if (event.shiftKey) outdentFragmentLines(frag, INDENT);
          else indentFragmentLines(frag, INDENT);

          const first = frag.firstChild;
          const last = frag.lastChild;

          range.insertNode(frag);

          // Восстановим выделение на вставленный фрагмент (как в редакторах)
          if (first && last) {
            const newRange = document.createRange();
            newRange.setStartBefore(first);
            newRange.setEndAfter(last);
            sel.removeAllRanges();
            sel.addRange(newRange);
          }

          handleInput();
          return;
        }

        // Обработка Cmd+V для вставки ссылок
        if (enableLinkInsertion) {
          const isModifierPressed = event.metaKey || event.ctrlKey;
          const isVPressed = event.key === "v" || event.key === "V";

          if (isModifierPressed && isVPressed && internalRef.current) {
            const selection = window.getSelection();
            if (selection && selection.rangeCount > 0) {
              const range = selection.getRangeAt(0);
              if (!range.collapsed) {
                // Есть выделенный текст
                shouldInsertLinkRef.current = true;
              }
            }
          } else {
            shouldInsertLinkRef.current = false;
          }
        }
      },
      [
        onKeyDown,
        value,
        enableLinkInsertion,
        handleInput,
        sanitizeMarkdownToHtml,
      ]
    );

    const handlePaste = useCallback(
      (event: React.ClipboardEvent<HTMLDivElement>) => {
        if (onPaste) {
          onPaste(event);
        }
        if (event.defaultPrevented || !internalRef.current) return;

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) return;

        const range = selection.getRangeAt(0);
        const clipboardText = event.clipboardData.getData("text/plain");

        if (
          shouldInsertLinkRef.current &&
          enableLinkInsertion &&
          !range.collapsed &&
          clipboardText
        ) {
          event.preventDefault();
          shouldInsertLinkRef.current = false;

          // Валидируем и нормализуем URL из буфера обмена
          const url = validateAndNormalizeUrl(clipboardText);
          if (!url) return;

          const selectedText = range.toString();
          if (!selectedText.trim()) return;

          // Создаем ссылку
          const link = createLinkElement(url, selectedText);

          // Заменяем выделенный текст на ссылку
          range.deleteContents();
          range.insertNode(link);

          // Обновляем курсор
          moveCursorAfterNode(link);
          handleInput();
          return;
        }

        shouldInsertLinkRef.current = false;
        event.preventDefault();

        if (!clipboardText) return;
        insertPlainTextAtRange({ range, selection, clipboardText });

        handleInput();
      },
      [shouldInsertLinkRef, enableLinkInsertion, onPaste, handleInput]
    );

    return (
      <>
        <div
          ref={setRefs}
          contentEditable
          suppressContentEditableWarning
          onInput={handleInput}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          onPaste={handlePaste}
          data-placeholder={placeholder}
          className={className}
          style={{
            minHeight: `${rows * 2.5}rem`,
            ...style,
          }}
        />
        {linkPreview && (
          <LinkPreview
            url={linkPreview.url}
            linkElement={linkPreview.linkElement}
            onClose={closeLinkPreview}
            onDelete={handleDeleteLink}
          />
        )}
      </>
    );
  }
);

MarkdownTextarea.displayName = "MarkdownTextarea";

export default MarkdownTextarea;
