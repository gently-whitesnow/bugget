import {
  Dispatch,
  RefObject,
  SetStateAction,
  useEffect,
  useMemo,
  useRef,
} from "react";
import { useLocation } from "react-router";
import { buildItemIdSet, parseHashNumbers } from "./hashScrollUtils";

type Options<T> = {
  parentId: number;
  items: T[];
  getItemId: (item: T) => number | undefined;
  hashPattern: RegExp;
  getElementId: (parentId: number, itemId: number) => string;
  setIsExpanded: (value: boolean) => void;
  setHighlightedId: Dispatch<SetStateAction<number | null>>;
  scrollContainerRef?: RefObject<HTMLElement | null>;
  scrollDelay?: number;
  scrollOffset?: number;
};

const getTargetTop = (
  element: HTMLElement,
  scrollOffset: number,
  container: HTMLElement | null
) => {
  if (!container) return { container: window, top: 0 };
  const containerRect = container.getBoundingClientRect();
  const elementRect = element.getBoundingClientRect();

  return {
    container,
    top: Math.max(
      0,
      elementRect.top - containerRect.top + container.scrollTop - scrollOffset
    ),
  };
};

/**
 * Хук для hash-навигации к вложенным сущностям вида #prefix-parentId-itemId.
 * При совпадении хэша раскрывает секцию, скроллит к элементу и временно подсвечивает его.
 */
export const useScrollToNestedHashHighlight = <T>({
  parentId,
  items,
  getItemId,
  hashPattern,
  getElementId,
  setIsExpanded,
  setHighlightedId,
  scrollContainerRef,
  scrollDelay = 0,
  scrollOffset = 2,
}: Options<T>) => {
  const location = useLocation();
  const scrollTimeoutRef = useRef<number | null>(null);
  const clearHighlightListenerRef = useRef<(() => void) | null>(null);
  const handledHashRef = useRef<string | null>(null);
  const itemIdSet = useMemo(
    () => buildItemIdSet(items, getItemId),
    [getItemId, items]
  );

  const clearHighlightListener = () => {
    if (clearHighlightListenerRef.current) {
      clearHighlightListenerRef.current();
      clearHighlightListenerRef.current = null;
    }
  };

  const clearScheduledScroll = () => {
    if (scrollTimeoutRef.current !== null) {
      window.clearTimeout(scrollTimeoutRef.current);
      scrollTimeoutRef.current = null;
    }
  };

  useEffect(() => {
    return () => {
      clearScheduledScroll();
      clearHighlightListener();
    };
  }, []);

  useEffect(() => {
    const hash = location.hash;
    if (!hash) return;

    if (handledHashRef.current === hash) {
      return;
    }

    const parsed = parseHashNumbers(hash, hashPattern);
    if (!parsed) return;

    const [hashParentId, hashItemId] = parsed;

    if (hashParentId !== parentId) return;
    if (!itemIdSet.has(hashItemId)) return;

    setIsExpanded(true);
    clearScheduledScroll();

    const scrollAndHighlight = () => {
      const element = document.getElementById(
        getElementId(hashParentId, hashItemId)
      );

      if (!element) {
        return;
      }

      const { container, top } = getTargetTop(
        element,
        scrollOffset,
        scrollContainerRef?.current ?? null
      );
      container.scrollTo({ top, behavior: "smooth" });

      setHighlightedId(hashItemId);
      handledHashRef.current = hash;
      clearHighlightListener();

      const onAnyClick = () => {
        setHighlightedId((current) =>
          current === hashItemId ? null : current
        );
        clearHighlightListener();
      };

      document.addEventListener("click", onAnyClick, true);
      clearHighlightListenerRef.current = () => {
        document.removeEventListener("click", onAnyClick, true);
      };
    };

    scrollTimeoutRef.current = window.setTimeout(() => {
      scrollTimeoutRef.current = null;
      scrollAndHighlight();
    }, scrollDelay);
  }, [
    getElementId,
    hashPattern,
    itemIdSet,
    location.hash,
    parentId,
    scrollContainerRef,
    scrollDelay,
    scrollOffset,
    setHighlightedId,
    setIsExpanded,
  ]);
};
