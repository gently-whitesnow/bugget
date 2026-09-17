import {
  useEffect,
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
};

const boxClassName =
  "w-full self-stretch textarea textarea-bordered text-sm bg-base-100 px-4 py-2";

/** Без фокуса результат отрисован, по клику или Enter открывается исходный markdown. */
const ResultTextarea = ({
  value,
  placeholder,
  autoFocus,
  maxLength = resultMaxLength,
  onBlur,
  onInput,
  onPaste,
}: Props) => {
  // Store существующего бага обновляется только после сохранения — до него показываем черновик.
  const [draft, setDraft] = useState(value);
  const [isEditing, setIsEditing] = useState(autoFocus);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  const startEditing = () => setIsEditing(true);

  if (isEditing || !draft.trim()) {
    return (
      <MarkdownTextarea
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
        // Сохранение результата висит на blur, поэтому Enter просто снимает фокус.
        onSubmit={() => (document.activeElement as HTMLElement | null)?.blur()}
        rows={3}
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
      tabIndex={0}
      aria-label={`Редактировать: ${placeholder}`}
      className={`${boxClassName} h-auto min-h-[7.5rem] cursor-text`}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
    >
      <MarkdownText text={draft} />
    </div>
  );
};

export default ResultTextarea;
