import { useState, useRef, useEffect } from "react";
import { ArrowDownNarrowWide, ArrowUpWideNarrow } from "lucide-react";

import { SelectMenu, SelectTrigger } from "../SelectPrimitives";

export type SortOption<T = string> = {
  label: string;
  value: T;
};

type Props<T = string> = {
  options: SortOption<T>[];
  value: T;
  direction: "asc" | "desc";
  onChange: (value: T) => void;
  onToggleDirection: () => void;
  className?: string;
};

const SortDropdown = <T,>({
  options,
  value,
  direction,
  onChange,
  onToggleDirection,
  className = "",
}: Props<T>) => {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const selectedLabel = options.find((opt) => opt.value === value)?.label;

  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };

    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  return (
    <div ref={ref} className={`flex rounded-box bg-transparent ${className}`}>
      <div className="relative">
        <SelectTrigger
          className="w-full justify-between whitespace-nowrap rounded-l-box px-3 py-1.5 text-sm font-medium hover:bg-base-200/70"
          fullWidth
          onClick={() => setOpen(!open)}
          placeholder="Сортировка"
        >
          {selectedLabel}
        </SelectTrigger>

        {open && (
          <SelectMenu
            className="z-50 mt-1 w-full p-1 shadow-lg"
            items={options}
            renderItem={(opt) => (
              <li key={String(opt.value)}>
                <button
                  type="button"
                  className={`w-full cursor-pointer rounded-box px-4 py-2 text-left text-sm transition-colors hover:bg-base-200 ${
                    opt.value === value ? "bg-base-200 font-semibold" : ""
                  }`}
                  onClick={() => {
                    onChange(opt.value);
                    setOpen(false);
                  }}
                >
                  {opt.label}
                </button>
              </li>
            )}
          />
        )}
      </div>

      <button
        type="button"
        className="cursor-pointer rounded-r-box px-2.5 py-1.5 text-base-content transition-colors hover:bg-base-200/70"
        onClick={onToggleDirection}
      >
        {direction === "asc" ? (
          <ArrowUpWideNarrow className="h-4 w-4" />
        ) : (
          <ArrowDownNarrowWide className="h-4 w-4" />
        )}
      </button>
    </div>
  );
};

export default SortDropdown;
