import { useEffect, useRef } from "react";
import { useLocation } from "react-router";

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
  const hasScrolled = useRef(false);

  // сбрасываем флаг при смене resetKey
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
