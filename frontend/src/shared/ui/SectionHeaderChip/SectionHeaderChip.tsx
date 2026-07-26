import { ReactNode } from "react";

type Props = {
  count: number;
  icon: ReactNode;
  texts: {
    zero: string;
    one: string;
    few: string;
    many: string;
  };
  onClick?: () => void;
  className?: string;
  disabled?: boolean;
};

export const SectionHeaderChip = ({
  count,
  icon,
  texts,
  onClick,
  className = "",
  disabled = false,
}: Props) => {
  const label =
    count === 0
      ? texts.zero
      : count === 1
        ? texts.one
        : count < 5
          ? texts.few
          : texts.many;

  return (
    <div
      className={`flex items-center gap-2 p-2 bg-base-200 rounded-lg w-fit mb-2 ${
        onClick && !disabled
          ? "cursor-pointer hover:bg-base-300 transition-colors"
          : ""
      } ${disabled ? "opacity-50 cursor-not-allowed" : ""} ${className}`}
      onClick={disabled ? undefined : onClick}
    >
      <div className="w-5 h-5 rounded-full bg-info/20 flex items-center justify-center">
        {icon}
      </div>
      <span className="text-sm font-medium text-base-content">
        {count === 0 ? label : `${count} ${label}`}
      </span>
    </div>
  );
};

export default SectionHeaderChip;
