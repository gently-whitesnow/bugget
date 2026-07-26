import { LucideIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import { SelectMenu, SelectTrigger } from "../SelectPrimitives";
import StatusOptionContent from "./components/StatusOptionContent";
import StatusOptionRow from "./components/StatusOptionRow";

export type StatusOption<T> = {
  value: T;
  label: string;
  description?: string;
  icon: LucideIcon;
  iconClassName: string;
  activeClassName: string;
};

type Props<T> = {
  status: T;
  options: StatusOption<T>[];
  onChange: (status: T) => void;
  className?: string;
  menuClassName?: string;
  fullWidth?: boolean;
};

const EntityStatusSelect = <T,>({
  status,
  options,
  onChange,
  className = "",
  menuClassName = "",
  fullWidth,
}: Props<T>) => {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const currentOption = options.find((option) => option.value === status);

  useEffect(() => {
    const handleOutsideClick = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };

    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  const handleSelect = (value: T) => {
    onChange(value);
    setOpen(false);
  };
  const triggerTooltip = currentOption?.description && !open;
  const resolvedFullWidth = fullWidth ?? false;

  return (
    <div ref={ref} className={`relative ${className}`}>
      <SelectTrigger
        className="bg-transparent hover:bg-base-200/70"
        fullWidth={resolvedFullWidth}
        onClick={() => setOpen((prev) => !prev)}
        placeholder="Выберите статус"
        title={triggerTooltip ? currentOption.description : undefined}
      >
        {currentOption ? (
          <div
            className={`flex min-w-0 flex-1 items-center rounded-box bg-transparent group-hover:bg-base-200/70`}
          >
            <StatusOptionContent
              label={currentOption.label}
              description={currentOption.description}
              icon={currentOption.icon}
              iconClassName={currentOption.iconClassName}
              compact={true}
              showDescription={false}
            />
          </div>
        ) : undefined}
      </SelectTrigger>

      {open && (
        <SelectMenu
          className={menuClassName}
          items={options}
          listClassName="flex flex-col gap-1"
          renderItem={(option) => (
            <StatusOptionRow
              key={String(option.value)}
              option={option}
              isSelected={option.value === status}
              onSelect={handleSelect}
            />
          )}
        />
      )}
    </div>
  );
};

export default EntityStatusSelect;
