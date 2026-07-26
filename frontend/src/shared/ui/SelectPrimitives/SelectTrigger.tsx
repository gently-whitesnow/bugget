import { ReactNode } from "react";

import SelectFieldLayout from "./SelectFieldLayout";

type Props = {
  children?: ReactNode;
  className?: string;
  fullWidth?: boolean;
  onClick?: () => void;
  placeholder?: string;
  rightSlot?: ReactNode;
  title?: string;
};

const SelectTrigger = ({
  children,
  className = "",
  fullWidth = false,
  onClick,
  placeholder = "Выберите значение",
  rightSlot,
  title,
}: Props) => {
  const trigger = (
    <button
      type="button"
      className={`group inline-flex cursor-pointer rounded-box p-1.5 transition-colors duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25 ${
        fullWidth ? "w-full" : "w-auto"
      } ${className}`}
      onClick={onClick}
      title={title}
    >
      <SelectFieldLayout placeholder={placeholder} rightSlot={rightSlot}>
        {children}
      </SelectFieldLayout>
    </button>
  );

  if (!title) {
    return trigger;
  }

  return (
    <div className="tooltip tooltip-bottom w-full" data-tip={title}>
      {trigger}
    </div>
  );
};

export default SelectTrigger;
