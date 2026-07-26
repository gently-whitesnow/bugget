import { useEffect, useMemo, useRef } from "react";
import { useLocation } from "react-router-dom";
import { buildItemIdSet, parseHashNumbers } from "./hashScrollUtils";

type Options<T> = {
  /**
   * Список элементов, в которых нужно искать целевой элемент.
   * Скролл произойдёт только когда элемент с нужным id будет найден в этом списке.
   */
  items: T[];
  /**
   * Функция для получения id элемента из item.
   * Возвращает undefined для элементов, к которым нельзя скроллить.
   */
  getId: (item: T) => number | undefined;
  /**
   * Паттерн хэша для извлечения id (например, /^#bug-(\d+)$/).
   * Первая группа захвата должна содержать id.
   */
  hashPattern: RegExp;
  /**
   * Ключ для сброса флага скролла (например, reportId).
   * При изменении этого значения скролл снова станет возможным.
   */
  resetKey?: string | number | null;
  /**
   * Задержка перед скроллом в мс (по умолчанию 100).
   * Нужна для стабилизации layout после рендеринга.
   */
  delay?: number;
};

/**
 * Хук для автоматического скролла к элементу по хэшу в URL.
 * Скролл происходит один раз после загрузки данных.
 */
export const useScrollToHash = <T>({
  items,
  getId,
  hashPattern,
  resetKey,
  delay = 100,
}: Options<T>) => {
  const location = useLocation();
  const hasScrolledRef = useRef(false);
  const itemIdSet = useMemo(() => buildItemIdSet(items, getId), [getId, items]);

  useEffect(() => {
    hasScrolledRef.current = false;
  }, [resetKey]);

  useEffect(() => {
    const hash = location.hash;
    if (!hash || hasScrolledRef.current) return;

    const parsed = parseHashNumbers(hash, hashPattern);
    if (!parsed) return;

    const [targetId] = parsed;
    if (!itemIdSet.has(targetId)) return;

    const scrollToTarget = () => {
      const element = document.getElementById(hash.slice(1));
      if (!element) return;

      hasScrolledRef.current = true;
      element.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    if (delay <= 0) {
      scrollToTarget();
      return;
    }

    const timeoutId = window.setTimeout(scrollToTarget, delay);
    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [delay, hashPattern, itemIdSet, location.hash]);
};
