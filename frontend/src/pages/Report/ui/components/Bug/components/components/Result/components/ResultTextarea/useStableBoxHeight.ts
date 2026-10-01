import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { RefObject } from "react";

/** Пауза в наборе, после которой пересчитываем разметку. Парсить её на каждое нажатие дорого. */
const measureDelay = 400;

type StableBoxHeight = {
  /** Высота, одинаковая для просмотра и правки; undefined — считать нечего. */
  height: string | undefined;
  /** Поле с исходником: настоящее в правке, невидимая копия в просмотре. */
  sourceRef: RefObject<HTMLTextAreaElement | null>;
  /** Разметка: настоящая в просмотре, невидимая копия в правке. */
  renderedRef: RefObject<HTMLDivElement | null>;
  /** Запомнить живую высоту элемента перед переключением режима. */
  keepHeightOf: (element: HTMLElement | null) => void;
  /** Текст для невидимой копии разметки: отстаёт от ввода, чтобы не парсить каждое нажатие. */
  measuredText: string;
};

/**
 * Один и тот же текст занимает разную высоту разметкой и исходником, поэтому коробка
 * берёт большую из двух: иначе она прыгает на фокусе и расфокусе. На экране всегда есть
 * только одно из двух, поэтому второе держим невидимой копией — ровно одной за раз.
 */
export const useStableBoxHeight = (
  text: string,
  isEditing: boolean
): StableBoxHeight => {
  const sourceRef = useRef<HTMLTextAreaElement>(null);
  const renderedRef = useRef<HTMLDivElement>(null);
  const [height, setHeight] = useState<string>();
  const [measuredText, setMeasuredText] = useState(text);

  useEffect(() => {
    if (text === measuredText) return;
    const timer = setTimeout(() => setMeasuredText(text), measureDelay);
    return () => clearTimeout(timer);
  }, [text, measuredText]);

  // measuredText в зависимостях: копия разметки перерисовывается после паузы в наборе,
  // и без этого поправку приносил бы только ResizeObserver.
  useLayoutEffect(() => {
    // Текст стёрли целиком: мерить нечего, а прошлая высота держала бы пустое поле.
    if (!text.trim()) {
      setHeight(undefined);
      return;
    }

    const source = sourceRef.current;
    const rendered = renderedRef.current;
    if (!source || !rendered) return;

    const measure = () => {
      const styles = getComputedStyle(source);
      // scrollHeight включает паддинги, но не бордер.
      const border = source.offsetHeight - source.clientHeight;
      // В окружении без вёрстки паддинги пустые: NaN сломал бы max() в стиле.
      const padding =
        (parseFloat(styles.paddingTop) || 0) +
        (parseFloat(styles.paddingBottom) || 0);
      const frame = border + padding;

      // У настоящего поля высота уже подогнана под текст, поэтому меряем его при
      // сброшенном min-height, иначе прошлое значение не даст ему сжаться.
      const restore = source.style.minHeight;
      source.style.minHeight = "0px";
      const sourceHeight = source.scrollHeight + border;
      source.style.minHeight = restore;

      const renderedHeight = rendered.getBoundingClientRect().height + frame;
      setHeight(`${Math.ceil(Math.max(sourceHeight, renderedHeight))}px`);
    };

    measure();
    if (typeof ResizeObserver === "undefined") return;

    // Разметка приезжает отдельным чанком и меняет высоту уже после первого замера.
    const observer = new ResizeObserver(measure);
    observer.observe(rendered);
    return () => observer.disconnect();
  }, [text, measuredText, isEditing]);

  const keepHeightOf = (element: HTMLElement | null) => {
    const live = element?.getBoundingClientRect().height;
    if (live) setHeight(`${Math.ceil(live)}px`);
  };

  return { height, sourceRef, renderedRef, keepHeightOf, measuredText };
};
