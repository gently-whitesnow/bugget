import { createContext, RefObject, useContext } from "react";

type LayoutContextValue = {
  /** Скрыт ли хэдер в данный момент */
  isHeaderHidden: boolean;
  /** Основной скролл-контейнер layout */
  scrollContainerRef: RefObject<HTMLDivElement | null>;
};

export const LayoutContext = createContext<LayoutContextValue>({
  isHeaderHidden: false,
  scrollContainerRef: { current: null },
});

/**
 * Хук для доступа к состоянию Layout (видимость хэдера и т.д.)
 */
export const useLayout = () => useContext(LayoutContext);
