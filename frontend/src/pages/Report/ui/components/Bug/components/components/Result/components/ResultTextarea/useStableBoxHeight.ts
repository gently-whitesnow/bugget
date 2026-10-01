import { useLayoutEffect, useRef, useState, type RefObject } from "react";

type StableBoxHeight = {
  /** Высота, одинаковая для просмотра и правки; undefined — считать нечего. */
  height: string | undefined;
  /** Ссылки на то, по чему меряем: невидимая копия поля и отрисованная разметка. */
  sourceRef: RefObject<HTMLTextAreaElement | null>;
  renderedRef: RefObject<HTMLDivElement | null>;
  /** Запомнить живую высоту элемента перед переключением режима. */
  keepHeightOf: (element: HTMLElement | null) => void;
};

/**
 * Один и тот же текст занимает разную высоту разметкой и исходником, поэтому коробка
 * берёт большую из двух: иначе она прыгает на фокусе и расфокусе.
 */
export const useStableBoxHeight = (
  text: string,
  isEditing: boolean
): StableBoxHeight => {
  const sourceRef = useRef<HTMLTextAreaElement>(null);
  const renderedRef = useRef<HTMLDivElement>(null);
  const [height, setHeight] = useState<string>();

  useLayoutEffect(() => {
    const source = sourceRef.current;
    const rendered = renderedRef.current;
    if (!source || !rendered) {
      // Текст стёрли целиком: мерить нечего, а прошлая высота держала бы пустое поле.
      if (!text.trim()) setHeight(undefined);
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
      const sourceHeight = source.scrollHeight + border;
      const renderedHeight = rendered.getBoundingClientRect().height + frame;
      setHeight(`${Math.ceil(Math.max(sourceHeight, renderedHeight))}px`);
    };

    measure();
    if (typeof ResizeObserver === "undefined") return;

    // Разметка приезжает отдельным чанком и меняет высоту уже после первого замера.
    const observer = new ResizeObserver(measure);
    observer.observe(rendered);
    return () => observer.disconnect();
  }, [text, isEditing]);

  const keepHeightOf = (element: HTMLElement | null) => {
    const live = element?.getBoundingClientRect().height;
    if (live) setHeight(`${Math.ceil(live)}px`);
  };

  return { height, sourceRef, renderedRef, keepHeightOf };
};
