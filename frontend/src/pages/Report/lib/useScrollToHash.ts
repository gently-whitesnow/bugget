import { useEffect, useMemo, useRef } from "react";
import { useLocation } from "react-router";
import { buildItemIdSet, parseHashNumbers } from "./hashScrollUtils";

type Options<T> = {
  /** Скролл произойдёт, только когда целевой id найден в этом списке. */
  items: T[];
  /** Возвращает undefined для элементов, к которым нельзя скроллить. */
  getId: (item: T) => number | undefined;
  /** Первая группа захвата должна содержать id: /^#bug-(\d+)$/. */
  hashPattern: RegExp;
  /** При смене ключа (например, reportId) скролл снова возможен. */
  resetKey?: string | number | null;
  /** Задержка в мс для стабилизации layout после рендера. */
  delay?: number;
};

/** Однократный скролл к элементу из хэша URL после загрузки данных. */
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
