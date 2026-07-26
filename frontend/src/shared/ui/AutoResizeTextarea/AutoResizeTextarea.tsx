import { forwardRef, useCallback, useEffect, useRef } from "react";

type Props = {
  value: string;
  onChange: (value: string) => void;
  onBlur?: (value: string) => void;
  onSave?: (value: string) => void;
  onCancel?: (value: string) => void;
  rollbackValue?: string;
  autoFocus?: boolean;
  onFocus?: (value: string) => void;
  onKeyDown?: React.KeyboardEventHandler<HTMLTextAreaElement>;
  placeholder?: string;
  className?: string;
  maxLength?: number;
  rows?: number;
};

const AutoResizeTextarea = forwardRef<HTMLTextAreaElement, Props>(
  (
    {
      value,
      onChange,
      onBlur,
      onSave,
      onCancel,
      rollbackValue,
      autoFocus = false,
      onFocus,
      onKeyDown,
      placeholder,
      className = "",
      maxLength,
      rows = 1,
    },
    ref
  ) => {
    const textareaRef = useRef<HTMLTextAreaElement | null>(null);
    const focusValueRef = useRef(value);
    const skipBlurRef = useRef(false);
    const lastEmittedValueRef = useRef(value);

    const setRefs = useCallback(
      (node: HTMLTextAreaElement | null) => {
        textareaRef.current = node;
        if (!ref) return;

        if (typeof ref === "function") {
          ref(node);
        } else {
          ref.current = node;
        }
      },
      [ref]
    );

    const adjustHeight = useCallback(() => {
      const el = textareaRef.current;
      if (!el) return;
      el.style.height = "0px";
      el.style.height = `${el.scrollHeight}px`;
    }, []);

    useEffect(() => {
      const el = textareaRef.current;
      if (!el) return;

      if (value !== lastEmittedValueRef.current) {
        el.value = value;
        lastEmittedValueRef.current = value;
      }

      adjustHeight();
    }, [value, adjustHeight]);

    useEffect(() => {
      if (!autoFocus || !textareaRef.current) return;

      textareaRef.current.focus();
      textareaRef.current.setSelectionRange(
        textareaRef.current.value.length,
        textareaRef.current.value.length
      );
    }, [autoFocus]);

    const handleFocus = useCallback(() => {
      const currentValue = textareaRef.current?.value ?? value;
      focusValueRef.current = currentValue;
      onFocus?.(currentValue);
    }, [onFocus, value]);

    const handleBlur = useCallback(() => {
      if (skipBlurRef.current) {
        skipBlurRef.current = false;
        return;
      }

      const currentValue = textareaRef.current?.value ?? value;
      onBlur?.(currentValue);
    }, [onBlur, value]);

    const handleChange = useCallback(
      (e: React.ChangeEvent<HTMLTextAreaElement>) => {
        lastEmittedValueRef.current = e.target.value;
        onChange(e.target.value);
        adjustHeight();
      },
      [onChange, adjustHeight]
    );

    const handleKeyDown = useCallback(
      (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
        onKeyDown?.(event);
        if (event.defaultPrevented || event.nativeEvent.isComposing) return;

        if (event.key === "Enter" && !event.shiftKey) {
          event.preventDefault();
          skipBlurRef.current = true;
          onSave?.(textareaRef.current?.value ?? value);
          textareaRef.current?.blur();
          return;
        }

        if (event.key === "Escape") {
          event.preventDefault();
          const nextValue = rollbackValue ?? focusValueRef.current;
          skipBlurRef.current = true;
          if (textareaRef.current) {
            textareaRef.current.value = nextValue;
          }
          lastEmittedValueRef.current = nextValue;
          if (nextValue !== value) {
            onChange(nextValue);
          }
          onCancel?.(nextValue);
          textareaRef.current?.blur();
        }
      },
      [onKeyDown, onSave, value, rollbackValue, onChange, onCancel]
    );

    return (
      <textarea
        ref={setRefs}
        defaultValue={value}
        onChange={handleChange}
        onBlur={handleBlur}
        onFocus={handleFocus}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        className={`resize-none overflow-hidden ${className}`.trim()}
        maxLength={maxLength}
        rows={rows}
        autoFocus={autoFocus}
      />
    );
  }
);

AutoResizeTextarea.displayName = "AutoResizeTextarea";

export default AutoResizeTextarea;
