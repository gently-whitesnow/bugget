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
  onPaste?: (event: React.ClipboardEvent<HTMLTextAreaElement>) => void;
};

const ResultTextarea = forwardRef<HTMLTextAreaElement, Props>(
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
        // Сохранение результата висит на blur, поэтому Enter просто снимает фокус.
        onSubmit={() => (document.activeElement as HTMLElement | null)?.blur()}
        rows={3}
        className="w-full textarea textarea-bordered text-sm bg-base-100 min-h-[2.5rem] px-4 py-2 break-words focus:outline-none focus:ring-primary focus:ring-offset-0"
      />
    );
  }
);

ResultTextarea.displayName = "ResultTextarea";

export default ResultTextarea;
