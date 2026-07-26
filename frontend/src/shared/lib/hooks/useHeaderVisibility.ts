import { useEffect, useRef, useState, RefObject } from "react";

type UseHeaderVisibilityOptions = {
  /**
   * Зона в пикселях от верха страницы, где хэдер всегда виден.
   * Пока scrollTop < этого значения — хэдер показывается.
   * @default 600
   */
  alwaysVisibleZone?: number;

  /**
   * Минимальная дельта скролла вниз (в пикселях), чтобы скрыть хэдер.
   * Чем больше значение — тем "ленивее" хэдер скрывается.
   * @default 5
   */
  hideThreshold?: number;

  /**
   * Минимальная дельта скролла вверх (в пикселях), чтобы показать хэдер.
   * Чем больше значение — тем "ленивее" хэдер появляется.
   * @default 40
   */
  showThreshold?: number;
};

type UseHeaderVisibilityResult = {
  /** Скрыт ли хэдер в данный момент */
  isHidden: boolean;
  /** Ref для scroll-контейнера */
  scrollRef: RefObject<HTMLDivElement>;
};

/**
 * Хук для управления видимостью хэдера при скролле.
 *
 * - Скролл вниз → хэдер скрывается
 * - Скролл вверх → хэдер появляется
 * - В верхней зоне страницы → хэдер всегда виден
 */
export const useHeaderVisibility = (
  options: UseHeaderVisibilityOptions = {}
): UseHeaderVisibilityResult => {
  const {
    alwaysVisibleZone = 600,
    hideThreshold = 5,
    showThreshold = 40,
  } = options;

  const [isHidden, setIsHidden] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const lastScrollTop = useRef(0);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;

    const handleScroll = () => {
      const currentScroll = el.scrollTop;
      const delta = currentScroll - lastScrollTop.current;

      const isInAlwaysVisibleZone = currentScroll < alwaysVisibleZone;
      const isScrollingDown = delta > hideThreshold;
      const isScrollingUp = delta < -showThreshold;

      if (isInAlwaysVisibleZone) {
        setIsHidden(false);
      } else if (isScrollingDown) {
        setIsHidden(true);
      } else if (isScrollingUp) {
        setIsHidden(false);
      }

      lastScrollTop.current = currentScroll;
    };

    el.addEventListener("scroll", handleScroll);
    return () => el.removeEventListener("scroll", handleScroll);
  }, [alwaysVisibleZone, hideThreshold, showThreshold]);

  return { isHidden, scrollRef: scrollRef as RefObject<HTMLDivElement> };
};
