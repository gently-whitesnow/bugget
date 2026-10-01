import { resultMaxLength } from "@/shared/config";
import MarkdownEditor from "@/shared/ui/MarkdownEditor";

type Props = {
  value: string;
  placeholder: string;
  autoFocus: boolean;
  maxLength?: number;
  onBlur: (value: string) => void;
  onInput: (value: string) => void;
  onPaste?: (event: ClipboardEvent) => void;
  onAttachFile?: () => void;
  onEditingChange?: (isEditing: boolean) => void;
};

const boxClassName =
  "w-full self-stretch textarea textarea-bordered text-sm bg-base-100 px-4 py-2 min-h-[7.5rem]";

/**
 * Одна поверхность: разметка видна прямо в тексте, поэтому режимов просмотра и правки
 * больше нет — и нечему прыгать при фокусе.
 */
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
}: Props) => (
  <MarkdownEditor
    value={value}
    placeholder={placeholder}
    autoFocus={autoFocus}
    maxLength={maxLength}
    onInput={onInput}
    onPaste={onPaste}
    onFocus={() => onEditingChange?.(true)}
    onBlur={(next) => {
      onEditingChange?.(false);
      onBlur(next);
    }}
    actions={{
      // Результат — длинный текст с разметкой: Enter переносит строку, сохраняет ⌘Enter.
      submitOn: "modEnter",
      onSubmit: onBlur,
      onAttachFile,
    }}
    className={`${boxClassName} cursor-text overflow-hidden`}
  />
);

export default ResultTextarea;
