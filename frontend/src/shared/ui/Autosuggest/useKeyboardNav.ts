import { useCallback, useEffect, useState } from "react";

type Options<T> = {
  items: T[];
  isOpen: boolean;
  onSelect: (item: T) => void;
  onClose: () => void;
};

const useKeyboardNav = <T>({
  items,
  isOpen,
  onSelect,
  onClose,
}: Options<T>) => {
  const [activeIndex, setActiveIndex] = useState(-1);

  useEffect(() => {
    setActiveIndex(-1);
  }, [items, isOpen]);

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent) => {
      if (!isOpen) return;

      switch (event.key) {
        case "ArrowDown": {
          event.preventDefault();
          if (items.length === 0) return;
          setActiveIndex((prev) => (prev < items.length - 1 ? prev + 1 : 0));
          break;
        }
        case "ArrowUp": {
          event.preventDefault();
          if (items.length === 0) return;
          setActiveIndex((prev) => (prev <= 0 ? items.length - 1 : prev - 1));
          break;
        }
        case "Enter": {
          if (activeIndex >= 0 && activeIndex < items.length) {
            event.preventDefault();
            onSelect(items[activeIndex]);
          }
          break;
        }
        case "Escape": {
          event.preventDefault();
          onClose();
          break;
        }
      }
    },
    [isOpen, items, activeIndex, onSelect, onClose]
  );

  return { activeIndex, handleKeyDown };
};

export default useKeyboardNav;
