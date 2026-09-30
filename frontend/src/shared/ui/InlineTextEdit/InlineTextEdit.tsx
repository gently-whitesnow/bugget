import { useState } from "react";
import MarkdownTextarea from "@/shared/ui/MarkdownTextarea";

type Props = {
  initialValue: string;
  onSave: (text: string) => void;
  onCancel: () => void;
  onPaste?: (event: React.ClipboardEvent<HTMLTextAreaElement>) => void;
  onAttachFile?: () => void;
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
  onAttachFile,
  placeholder,
  rows = 2,
  className = "",
  autoFocus = true,
  maxLength,
}: Props) => {
  const [value, setValue] = useState(initialValue);

  return (
    <div className={`space-y-2 ${className}`}>
      <MarkdownTextarea
        value={value}
        onInput={setValue}
        onSubmit={onSave}
        onCancel={onCancel}
        onPaste={onPaste}
        onAttachFile={onAttachFile}
        className="textarea textarea-bordered w-full focus:outline-none"
        rows={rows}
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
