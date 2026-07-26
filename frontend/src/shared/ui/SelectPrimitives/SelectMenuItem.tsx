import { ReactNode } from "react";

type Props = {
  children: ReactNode;
  className?: string;
  contentClassName?: string;
  isSelected?: boolean;
  onClick?: () => void;
  rightSlot?: ReactNode;
};

const SelectMenuItem = ({
  children,
  className = "",
  contentClassName = "",
  isSelected = false,
  onClick,
  rightSlot,
}: Props) => {
  return (
    <li>
      <button
        type="button"
        className={`w-full cursor-pointer rounded-box px-4 py-2 text-left text-sm transition-colors hover:bg-base-200 ${
          isSelected ? "bg-base-200" : ""
        } ${className}`}
        onClick={onClick}
      >
        <div
          className={`flex items-center justify-between gap-3 ${contentClassName}`}
        >
          {children}
          {rightSlot ? <span className="shrink-0">{rightSlot}</span> : null}
        </div>
      </button>
    </li>
  );
};

export default SelectMenuItem;
