import { ReactNode } from "react";

type Props = {
  children?: ReactNode;
  className?: string;
  placeholder?: string;
  rightSlot?: ReactNode;
};

const SelectFieldLayout = ({
  children,
  className = "",
  placeholder = "Выберите значение",
  rightSlot,
}: Props) => {
  return (
    <div
      className={`flex w-full items-center justify-between rounded-box text-left ${className}`}
    >
      {children ?? (
        <span className="text-sm text-base-content/50">{placeholder}</span>
      )}
      {rightSlot ? (
        <span className="ml-auto flex items-center">{rightSlot}</span>
      ) : null}
    </div>
  );
};

export default SelectFieldLayout;
