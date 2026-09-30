import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
} from "react";
import { resultMaxLength } from "@/shared/config";
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
}: Props) => {
  // Store существующего бага обновляется только после сохранения — до него показываем черновик.
  const [draft, setDraft] = useState(value);
  const [isEditing, setIsEditing] = useState(autoFocus);
  const editorRef = useRef<HTMLTextAreaElement>(null);
  const sourceRef = useRef<HTMLTextAreaElement>(null);
  const renderedRef = useRef<HTMLDivElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  // Один и тот же текст занимает разную высоту разметкой и исходником: коробка берёт
  // большую из двух, иначе она прыгает на фокусе и расфокусе.
  const [boxHeight, setBoxHeight] = useState<string>();

  useEffect(() => {
    setDraft(value);
  }, [value]);

  useLayoutEffect(() => {
    const source = sourceRef.current;
    const rendered = renderedRef.current;
    if (!source || !rendered) {
      // Текст стёрли целиком: мерить нечего, а прошлая высота держала бы пустое поле.
      if (!draft.trim()) setBoxHeight(undefined);
      return;
    }

    const measure = () => {
      const styles = getComputedStyle(source);
      // scrollHeight включает паддинги, но не бордер.
      const border = source.offsetHeight - source.clientHeight;
      const frame =
        border +
        parseFloat(styles.paddingTop) +
        parseFloat(styles.paddingBottom);
      const height = Math.max(
        source.scrollHeight + border,
        rendered.getBoundingClientRect().height + frame
      );
      setBoxHeight(`${Math.ceil(height)}px`);
    };

    measure();
    if (typeof ResizeObserver === "undefined") return;

    // Разметка приезжает отдельным чанком и меняет высоту уже после первого замера.
    const observer = new ResizeObserver(measure);
    observer.observe(rendered);
    return () => observer.disconnect();
  }, [draft, isEditing]);

  // Esc возвращает фокус на коробку: иначе он уходит в начало страницы и Tab идёт не оттуда.
  const [returnFocus, setReturnFocus] = useState(false);

  useLayoutEffect(() => {
    if (isEditing || !returnFocus) return;
    boxRef.current?.focus();
    setReturnFocus(false);
  }, [isEditing, returnFocus]);

  const startEditing = () => {
    // Живой замер коробки: он точнее расчётного, если разметка ещё не домерялась.
    const height = boxRef.current?.getBoundingClientRect().height;
    if (height) setBoxHeight(`${Math.ceil(height)}px`);
    setIsEditing(true);
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
          setIsEditing(false);
          onBlur(next);
        }}
        onPaste={onPaste}
        onAttachFile={onAttachFile}
        onCancel={() => {
          setReturnFocus(true);
          // blur сохраняет результат и закрывает редактор — Esc ничего не отменяет.
          editorRef.current?.blur();
        }}
        // Результат — длинный текст с разметкой, поэтому Enter переносит строку,
        // а ⌘Enter снимает фокус: сохранение висит на blur.
        submitOnEnter={false}
        onSubmit={() => (document.activeElement as HTMLElement | null)?.blur()}
        rows={3}
        minHeight={boxHeight}
        className={`${boxClassName} focus:outline-none focus:ring-primary focus:ring-offset-0`}
      />
    );
  }

  const handleClick = (event: MouseEvent<HTMLDivElement>) => {
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
      aria-label={`Редактировать: ${placeholder}`}
      // Каретки в просмотре нет, поэтому фокус с клавиатуры показывает кольцо из DESIGN.
      className={`relative ${boxClassName} h-auto cursor-text focus-visible:ring-2 focus-visible:ring-primary/25`}
      style={boxHeight ? { minHeight: boxHeight } : undefined}
      // Без этого клик сперва подсвечивает коробку, и подсветка мигает при подмене на поле.
      onMouseDown={(event) => event.preventDefault()}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
    >
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
