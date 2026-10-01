import { useEffect, useRef } from "react";
import { useLocation } from "react-router";

type Options<T> = {
  /** Скролл произойдёт, только когда элемент с нужным id есть в списке. */
  items: T[];
  /** Возвращает undefined для элементов, к которым нельзя скроллить. */
  getId: (item: T) => number | undefined;
  /** Первая группа захвата должна содержать id: /^#bug-(\d+)$/. */
  hashPattern: RegExp;
  /** При смене ключа (например, reportId) скролл снова возможен. */
  resetKey?: string | number | null;
  /** Задержка в мс: даёт layout стабилизироваться после рендера. */
  delay?: number;
};

/** Скролл к элементу по хэшу в URL — один раз после загрузки данных. */
export const useScrollToHash = <T>({
  items,
  getId,
  hashPattern,
  resetKey,
  delay = 100,
}: Options<T>) => {
  const location = useLocation();
  const hasScrolled = useRef(false);

  useEffect(() => {
    hasScrolled.current = false;
  }, [resetKey]);

  useEffect(() => {
    const hash = location.hash;
    if (!hash || hasScrolled.current || items.length === 0) return;

    const match = hash.match(hashPattern);
    if (!match) return;

    const targetId = Number(match[1]);
    const itemExists = items.some((item) => getId(item) === targetId);
    if (!itemExists) return;

    const timeoutId = setTimeout(() => {
      const element = document.getElementById(hash.slice(1));
      if (element) {
        hasScrolled.current = true;
        element.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    }, delay);

    return () => clearTimeout(timeoutId);
  }, [location.hash, items, getId, hashPattern, delay]);
};
