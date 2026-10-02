import { useCallback, type RefObject } from "react";

/**
 * Высота поля под текст. Растит min-height, а не height: владелец может растянуть поле
 * выше текста, например выровнять по соседу в сетке.
 */
export const useAutoHeight = (
  ref: RefObject<HTMLTextAreaElement | null>,
  baseMinHeight: string
) =>
  useCallback(() => {
    const textarea = ref.current;
    if (!textarea) return;

    // Ноль, а не auto: у растянутого поля auto даёт высоту ячейки, а не текста.
    textarea.style.height = "0px";
    textarea.style.minHeight = "0px";
    // scrollHeight не включает бордер: без поправки последняя строка обрезается.
    const border = textarea.offsetHeight - textarea.clientHeight;
    const content = textarea.scrollHeight + border;

    textarea.style.minHeight = `max(${baseMinHeight}, ${content}px)`;
    textarea.style.height = "";
  }, [ref, baseMinHeight]);
