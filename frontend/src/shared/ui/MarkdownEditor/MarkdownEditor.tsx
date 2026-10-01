import { useEffect, useLayoutEffect, useRef } from "react";

import { history, historyKeymap } from "@codemirror/commands";
import { EditorState, type Extension } from "@codemirror/state";
import {
  EditorView,
  keymap,
  placeholder as cmPlaceholder,
} from "@codemirror/view";
import { isApplePlatform } from "@/shared/lib/keyboard";
import {
  linkFromPaste,
  resolveMarkdownKey,
  type SubmitMode,
} from "@/shared/lib/markdownEditing";
import { markdownDecorations } from "./markdownDecorations";
import { markdownLanguage } from "./markdownLanguage";
import { openLinkOnClick } from "./openLinkOnClick";
import { markdownLook } from "./markdownTheme";

type Actions = {
  onSubmit?: (value: string) => void;
  submitOn?: SubmitMode;
  onCancel?: () => void;
  onAttachFile?: () => void;
};

type Props = {
  value: string;
  placeholder?: string;
  autoFocus?: boolean;
  maxLength?: number;
  className?: string;
  onInput?: (value: string) => void;
  onBlur?: (value: string) => void;
  onFocus?: () => void;
  /** Вставка файлов и cURL: владелец решает, что делать с содержимым буфера. */
  onPaste?: (event: ClipboardEvent) => void;
  actions?: Actions;
};

/**
 * Одна поверхность вместо «исходник или разметка»: текст остаётся markdown, но заголовки,
 * жирное и ссылки видно сразу. Поэтому переключать режимы и мерить две высоты не нужно.
 */
const MarkdownEditor = ({
  value,
  placeholder = "",
  autoFocus = false,
  maxLength,
  className = "",
  onInput,
  onBlur,
  onFocus,
  onPaste,
  actions = {},
}: Props) => {
  const hostRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);
  // Обработчики читаем из ref: пересоздавать редактор на каждый рендер нельзя.
  const handlersRef = useRef({ onInput, onBlur, onFocus, onPaste, actions });
  handlersRef.current = { onInput, onBlur, onFocus, onPaste, actions };
  // Esc возвращает последний сохранённый текст, поэтому держим его отдельно от документа.
  const savedRef = useRef(value);
  savedRef.current = value;

  useLayoutEffect(() => {
    const host = hostRef.current;
    if (!host) return;

    const extensions: Extension[] = [
      history(),
      // Лимит длины: у textarea это был атрибут maxlength, здесь — фильтр транзакций.
      EditorState.changeFilter.of(
        (transaction) =>
          maxLength === undefined || transaction.newDoc.length <= maxLength
      ),
      keymap.of(historyKeymap),
      markdownLanguage,
      markdownDecorations,
      openLinkOnClick,
      EditorView.lineWrapping,
      cmPlaceholder(placeholder),
      markdownLook,
      EditorView.updateListener.of((update) => {
        if (update.docChanged) {
          handlersRef.current.onInput?.(update.state.doc.toString());
        }
      }),
      EditorView.domEventHandlers({
        focus: () => {
          handlersRef.current.onFocus?.();
          return false;
        },
        blur: (_, view) => {
          handlersRef.current.onBlur?.(view.state.doc.toString());
          return false;
        },
        paste: (event, view) => {
          // Сначала владелец: он забирает файлы и cURL из буфера.
          handlersRef.current.onPaste?.(event);
          if (event.defaultPrevented) return true;

          const { main } = view.state.selection;
          const edit = linkFromPaste(
            {
              value: view.state.doc.toString(),
              start: main.from,
              end: main.to,
            },
            event.clipboardData?.getData("text/plain") ?? ""
          );
          if (!edit) return false;

          // Вставка адреса поверх выделения делает из него ссылку, как в textarea.
          event.preventDefault();
          view.dispatch({
            changes: { from: edit.from, to: edit.to, insert: edit.insert },
            selection: { anchor: edit.selectionStart, head: edit.selectionEnd },
          });
          return true;
        },
        // Раскладка общая с textarea: те же чистые функции, тот же разбор клавиш.
        keydown: (event, view) => {
          const { actions: current } = handlersRef.current;

          if (event.key === "Escape") {
            event.preventDefault();
            view.dispatch({
              changes: {
                from: 0,
                to: view.state.doc.length,
                insert: savedRef.current,
              },
            });
            current.onCancel?.();
            view.contentDOM.blur();
            return true;
          }

          const { main } = view.state.selection;
          const action = resolveMarkdownKey(
            event,
            {
              value: view.state.doc.toString(),
              start: main.from,
              end: main.to,
            },
            {
              submit: current.onSubmit ? (current.submitOn ?? "enter") : "none",
              canAttach: Boolean(current.onAttachFile),
              isApple: isApplePlatform(),
            }
          );
          if (!action) return false;

          event.preventDefault();
          if (action.type === "submit") {
            current.onSubmit?.(view.state.doc.toString());
          }
          if (action.type === "attach") current.onAttachFile?.();
          if (action.type === "edit") {
            const { from, to, insert, selectionStart, selectionEnd } =
              action.edit;
            view.dispatch({
              changes: { from, to, insert },
              selection: { anchor: selectionStart, head: selectionEnd },
              scrollIntoView: true,
            });
          }
          return true;
        },
      }),
    ];

    const view = new EditorView({
      state: EditorState.create({ doc: value, extensions }),
      parent: host,
    });
    viewRef.current = view;

    return () => {
      view.destroy();
      viewRef.current = null;
    };
    // Редактор создаётся один раз: текст и обработчики обновляются отдельно.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const view = viewRef.current;
    if (!view || view.state.doc.toString() === value) return;
    view.dispatch({
      changes: { from: 0, to: view.state.doc.length, insert: value },
    });
  }, [value]);

  useEffect(() => {
    const view = viewRef.current;
    if (!autoFocus || !view) return;
    view.focus();
    view.dispatch({ selection: { anchor: view.state.doc.length } });
  }, [autoFocus]);

  return <div ref={hostRef} className={className} />;
};

export default MarkdownEditor;
