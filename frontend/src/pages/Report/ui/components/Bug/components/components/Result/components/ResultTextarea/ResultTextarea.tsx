import {
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
} from "react";
import { resultMaxLength } from "@/shared/config";
import { useStableBoxHeight } from "./useStableBoxHeight";
import { MarkdownText, MarkdownTextarea } from "@/shared/ui";

type Props = {
  value: string;
  placeholder: string;
  autoFocus: boolean;
  maxLength?: number;
  onBlur: (value: string) => void;
  onInput: (value: string) => void;
  onPaste?: (event: React.ClipboardEvent<HTMLTextAreaElement>) => void;
  onAttachFile?: () => void;
  /** Владелец показывает подсказку о клавишах, пока поле правят. */
  onEditingChange?: (isEditing: boolean) => void;
};

const boxClassName =
  "w-full self-stretch textarea textarea-bordered text-sm bg-base-100 px-4 py-2 min-h-[7.5rem]";

/** Без фокуса результат отрисован, по клику или Enter открывается исходный markdown. */
const ResultTextarea = ({
  value,
  placeholder,
  autoFocus,
  maxLength = resultMaxLength,
  onBlur,
  onInput,
  onPaste,
  onAttachFile,
  onEditingChange,
}: Props) => {
  // Store существующего бага обновляется только после сохранения — до него показываем черновик.
  const [draft, setDraft] = useState(value);
  const [isEditing, setIsEditing] = useState(autoFocus);
  const editorRef = useRef<HTMLTextAreaElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  const { height, sourceRef, renderedRef, keepHeightOf } = useStableBoxHeight(
    draft,
    isEditing
  );

  useEffect(() => {
    setDraft(value);
  }, [value]);

  // Esc возвращает фокус на коробку: иначе он уходит в начало страницы и Tab идёт не оттуда.
  const returnFocusRef = useRef(false);

  useLayoutEffect(() => {
    if (isEditing || !returnFocusRef.current) return;
    returnFocusRef.current = false;
    boxRef.current?.focus();
  }, [isEditing]);

  const hintId = useId();

  const changeEditing = (next: boolean) => {
    setIsEditing(next);
    onEditingChange?.(next);
  };

  const startEditing = () => {
    if (isEditing) return;
    // Живой замер коробки точнее расчётного, если разметка ещё не домерялась.
    keepHeightOf(boxRef.current);
    changeEditing(true);
  };

  if (isEditing || !draft.trim()) {
    return (
      <MarkdownTextarea
        ref={editorRef}
        value={draft}
        placeholder={placeholder}
        autoFocus={isEditing}
        maxLength={maxLength}
        onFocus={startEditing}
        onInput={(next) => {
          setDraft(next);
          onInput(next);
        }}
        onBlur={(next) => {
          setDraft(next);
          changeEditing(false);
          onBlur(next);
        }}
        onPaste={onPaste}
        actions={{
          // Результат — длинный текст с разметкой, поэтому Enter переносит строку,
          // а ⌘Enter снимает фокус: сохранение висит на blur.
          submitOn: "modEnter",
          onSubmit: () => editorRef.current?.blur(),
          onCancel: () => {
            returnFocusRef.current = true;
            // blur сохраняет результат и закрывает редактор — Esc ничего не отменяет.
            editorRef.current?.blur();
          },
          onAttachFile,
        }}
        rows={3}
        minHeight={height}
        className={`${boxClassName} focus:outline-none focus:ring-primary focus:ring-offset-0`}
      />
    );
  }

  const handleClick = (event: MouseEvent<HTMLDivElement>) => {
    // По ссылке переходят, а не правят текст под ней.
    if ((event.target as HTMLElement).closest("a")) return;
    // Выделение текста мышью — не повод открывать редактор.
    if (window.getSelection()?.toString()) return;
    event.preventDefault();
    startEditing();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget || event.key !== "Enter") return;
    event.preventDefault();
    startEditing();
  };

  return (
    <div
      ref={boxRef}
      tabIndex={0}
      aria-label={placeholder}
      aria-describedby={hintId}
      // Каретки в просмотре нет, поэтому фокус с клавиатуры показывает кольцо из DESIGN.
      className={`relative ${boxClassName} h-auto cursor-text focus-visible:ring-2 focus-visible:ring-primary/25`}
      style={height ? { minHeight: height } : undefined}
      // Без этого клик сперва подсвечивает коробку, и подсветка мигает при подмене на поле.
      onMouseDown={(event) => event.preventDefault()}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
    >
      {/* Роль кнопки поставить нельзя — внутри ссылки, поэтому подсказываем текстом. */}
      <span id={hintId} className="sr-only">
        Нажмите Enter, чтобы редактировать.
      </span>
      {/* Невидимая копия поля: по ней меряется высота исходника, вёрстку она не двигает. */}
      <textarea
        ref={sourceRef}
        value={draft}
        readOnly
        aria-hidden
        tabIndex={-1}
        className={`${boxClassName} pointer-events-none invisible absolute inset-x-0 top-0 resize-none overflow-hidden`}
      />
      {/* flow-root: без него отступы разметки выходят за замеряемый блок. */}
      <div ref={renderedRef} className="flow-root">
        <MarkdownText text={draft} />
      </div>
    </div>
  );
};

export default ResultTextarea;
