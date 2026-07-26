import { useState, KeyboardEvent, useRef } from "react";
import MarkdownTextarea from "@/shared/ui/MarkdownTextarea";

type Props = {
  initialValue: string;
  onSave: (text: string) => void;
  onCancel: () => void;
  onPaste?: (event: React.ClipboardEvent<HTMLDivElement>) => void;
  placeholder?: string;
  rows?: number;
  className?: string;
  autoFocus?: boolean;
  maxLength?: number;
};

const InlineTextEdit = ({
  initialValue,
  onSave,
  onCancel,
  onPaste,
  placeholder,
  rows = 2,
  className = "",
  autoFocus = true,
  maxLength,
}: Props) => {
  const [value, setValue] = useState(initialValue);
  const textareaRef = useRef<HTMLDivElement>(null);

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      onSave(value);
    } else if (e.key === "Escape") {
      e.preventDefault();
      onCancel();
    }
  };

  return (
    <div className={`space-y-2 ${className}`}>
      <MarkdownTextarea
        ref={textareaRef}
        value={value}
        onInput={setValue}
        onKeyDown={handleKeyDown}
        onPaste={onPaste}
        className="textarea textarea-bordered w-full resize-none focus:outline-none"
        style={{ minHeight: `${rows * 2.5}rem` }}
        placeholder={placeholder}
        maxLength={maxLength}
        autoFocus={autoFocus}
      />
      <div className="flex gap-2">
        <button
          className="btn btn-sm btn-primary"
          onClick={() => onSave(value)}
        >
          Сохранить
        </button>
        <button className="btn btn-sm btn-ghost" onClick={onCancel}>
          Отмена
        </button>
      </div>
    </div>
  );
};

export default InlineTextEdit;
