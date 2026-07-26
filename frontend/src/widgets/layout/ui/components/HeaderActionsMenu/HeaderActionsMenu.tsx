import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Menu } from "lucide-react";

import "./HeaderActionsMenu.css";

type Props = {
  children: ReactNode;
};

const HeaderActionsMenu = ({ children }: Props) => {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent | TouchEvent) => {
      if (menuRef.current?.contains(event.target as Node)) return;
      setIsOpen(false);
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setIsOpen(false);
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("touchstart", handleClickOutside);
    document.addEventListener("keydown", handleEscape);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("touchstart", handleClickOutside);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [isOpen]);

  return (
    <div ref={menuRef} className="header-actions-menu">
      <button
        type="button"
        className="btn btn-square bg-base-100"
        aria-label={isOpen ? "Закрыть меню действий" : "Открыть меню действий"}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        onClick={() => setIsOpen((current) => !current)}
      >
        <Menu className="h-4 w-4" />
      </button>

      {isOpen && (
        <div
          className="header-actions-panel z-[250] rounded-box border border-base-300 bg-base-100 p-2 shadow-lg"
          role="menu"
          onClick={() => setIsOpen(false)}
        >
          <div className="flex flex-col gap-2">{children}</div>
        </div>
      )}
    </div>
  );
};

export default HeaderActionsMenu;
