import { useEffect, useRef, useState, RefObject } from "react";

type UseHeaderVisibilityOptions = {
  /** Зона от верха (px), где хэдер всегда виден. По умолчанию 600. */
  alwaysVisibleZone?: number;

  /** Дельта скролла вниз (px), после которой хэдер скрывается. */
  hideThreshold?: number;

  /** Дельта скролла вверх (px), после которой хэдер появляется. */
  showThreshold?: number;
};

type UseHeaderVisibilityResult = {
  isHidden: boolean;
  scrollRef: RefObject<HTMLDivElement>;
};

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
