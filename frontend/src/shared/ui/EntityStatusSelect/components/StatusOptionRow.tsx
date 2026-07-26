import { Check } from "lucide-react";

import type { StatusOption } from "../EntityStatusSelect";
import StatusOptionContent from "./StatusOptionContent";

type Props<T> = {
  option: StatusOption<T>;
  isSelected: boolean;
  onSelect: (value: T) => void;
};

const StatusOptionRow = <T,>({ option, isSelected, onSelect }: Props<T>) => {
  return (
    <button
      type="button"
      className={`flex w-full cursor-pointer items-center justify-between rounded-box px-3 py-1 text-left transition-colors ${
        isSelected
          ? option.activeClassName
          : "text-base-content hover:bg-base-200"
      }`}
      onClick={() => onSelect(option.value)}
    >
      <StatusOptionContent
        label={option.label}
        description={option.description}
        icon={option.icon}
        iconClassName={option.iconClassName}
        truncateLabel={false}
      />
      <Check
        className={`h-4 w-4 shrink-0 ${
          isSelected ? "text-base-content/70" : "text-transparent"
        }`}
      />
    </button>
  );
};

export default StatusOptionRow;
