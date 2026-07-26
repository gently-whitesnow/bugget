import {
  ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { MoreVertical } from "lucide-react";

export type ActionItem = {
  id?: string | number;
  icon: ReactNode;
  label: string;
  onClick: () => void;
  className?: string;
};

type Props = {
  items: ActionItem[];
  triggerIcon?: ReactNode;
  triggerClassName?: string;
  menuPosition?:
    | "start"
    | "end"
    | "left"
    | "right"
    | "bottom-left"
    | "bottom-right";
  onOpenChange?: (isOpen: boolean) => void;
};

const standartGap = 8;

const ActionDropdown = ({
  items,
  triggerIcon,
  triggerClassName = "btn btn-ghost btn-xs p-1 text-base-content/70 hover:text-base-content",
  menuPosition = "right",
  onOpenChange,
}: Props) => {
  const [isOpen, setIsOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLUListElement>(null);
  // request animation frame id
  // нужен, чтобы не вызывать updateMenuPosition чаще, чем нужно
  const rafIdRef = useRef<number | null>(null);
  const lastPositionRef = useRef<{ top: number; left: number } | null>(null);

  const handleOpenChange = useCallback(
    (open: boolean) => {
      setIsOpen(open);
      onOpenChange?.(open);
    },
    [onOpenChange]
  );

  const updateMenuPosition = useCallback(() => {
    if (!isOpen || !triggerRef.current || !menuRef.current) return;

    const gap = standartGap;
    const viewportPadding = standartGap;
    const triggerRect = triggerRef.current.getBoundingClientRect();
    const menuRect = menuRef.current.getBoundingClientRect();

    let top = triggerRect.bottom + gap;
    let left = triggerRect.left;

    if (menuPosition === "end" || menuPosition === "bottom-left") {
      left = triggerRect.right - menuRect.width;
    }

    if (menuPosition === "left") {
      top = triggerRect.top;
      left = triggerRect.left - menuRect.width - gap;
    }

    if (menuPosition === "right") {
      top = triggerRect.top;
      left = triggerRect.right + gap;
    }

    const maxLeft = window.innerWidth - menuRect.width - viewportPadding;
    left = Math.min(
      Math.max(left, viewportPadding),
      Math.max(maxLeft, viewportPadding)
    );

    const maxTop = window.innerHeight - menuRect.height - viewportPadding;
    if (top + menuRect.height > window.innerHeight - viewportPadding) {
      const fallbackTop = triggerRect.top - menuRect.height - gap;
      top = fallbackTop >= viewportPadding ? fallbackTop : maxTop;
    }

    top = Math.max(viewportPadding, top);

    const previousPosition = lastPositionRef.current;
    if (
      previousPosition &&
      previousPosition.top === top &&
      previousPosition.left === left
    ) {
      return;
    }

    lastPositionRef.current = { top, left };
    menuRef.current.style.top = `${top}px`;
    menuRef.current.style.left = `${left}px`;
    menuRef.current.style.visibility = "visible";
  }, [isOpen, menuPosition]);

  useLayoutEffect(() => {
    if (!isOpen) return;

    updateMenuPosition();
  }, [isOpen, updateMenuPosition]);

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (e: MouseEvent | TouchEvent) => {
      const target = e.target as Node;
      if (
        menuRef.current?.contains(target) ||
        triggerRef.current?.contains(target)
      ) {
        return;
      }

      handleOpenChange(false);
    };

    const handleReposition = () => {
      if (rafIdRef.current !== null) return;

      rafIdRef.current = window.requestAnimationFrame(() => {
        rafIdRef.current = null;
        updateMenuPosition();
      });
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("touchstart", handleClickOutside);
    window.addEventListener("resize", handleReposition);
    window.addEventListener("scroll", handleReposition, true);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("touchstart", handleClickOutside);
      window.removeEventListener("resize", handleReposition);
      window.removeEventListener("scroll", handleReposition, true);
      if (rafIdRef.current !== null) {
        window.cancelAnimationFrame(rafIdRef.current);
        rafIdRef.current = null;
      }
      lastPositionRef.current = null;
    };
  }, [isOpen, handleOpenChange, updateMenuPosition]);

  const handleItemClick = (onClick: () => void) => {
    onClick();
    handleOpenChange(false);
  };

  const toggleMenu = (e: React.MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    handleOpenChange(!isOpen);
  };

  return (
    <div className="inline-flex">
      <button
        ref={triggerRef}
        type="button"
        className={`${triggerClassName} list-none cursor-pointer`}
        onClick={toggleMenu}
        aria-haspopup="menu"
        aria-expanded={isOpen}
      >
        {triggerIcon || <MoreVertical className="w-4 h-4" />}
      </button>

      {isOpen &&
        createPortal(
          <ul
            ref={menuRef}
            style={{ position: "fixed", visibility: "hidden", zIndex: 50 }}
            className="menu min-w-[min(100vw-1rem,10rem)] rounded-lg border border-base-300 bg-base-100 p-1 shadow-lg"
          >
            {items.map((item, index) => (
              <li key={item.id ?? `${item.label}-${index}`}>
                <button
                  className={item.className}
                  onClick={() => handleItemClick(item.onClick)}
                >
                  {item.icon}
                  {item.label}
                </button>
              </li>
            ))}
          </ul>,
          document.body
        )}
    </div>
  );
};

export default ActionDropdown;
