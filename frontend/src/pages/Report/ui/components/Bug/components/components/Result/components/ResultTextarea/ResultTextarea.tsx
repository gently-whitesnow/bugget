import { forwardRef } from "react";
import { resultMaxLength } from "@/shared/config";
import { MarkdownTextarea } from "@/shared/ui";

type Props = {
  value: string;
  placeholder: string;
  autoFocus: boolean;
  rows?: number;
  maxLength?: number;
  onBlur: (value: string) => void;
  onInput: (value: string) => void;
  onPaste?: (event: React.ClipboardEvent<HTMLDivElement>) => void;
};

const ResultTextarea = forwardRef<HTMLDivElement, Props>(
  (
    {
      value,
      placeholder,
      autoFocus,
      maxLength = resultMaxLength,
      onBlur,
      onInput,
      onPaste,
    },
    ref
  ) => {
    return (
      <MarkdownTextarea
        ref={ref}
        value={value}
        placeholder={placeholder}
        autoFocus={autoFocus}
        maxLength={maxLength}
        onBlur={onBlur}
        onInput={onInput}
        onPaste={onPaste}
        rows={3}
        className="w-full textarea textarea-bordered text-sm resize-none bg-base-100 overflow-y-hidden min-h-[2.5rem] px-4 py-2 whitespace-pre-wrap break-words focus:outline-none focus:ring-primary focus:ring-offset-0 empty:before:content-[attr(data-placeholder)] empty:before:text-base-content/40"
      />
    );
  }
);

ResultTextarea.displayName = "ResultTextarea";

export default ResultTextarea;
