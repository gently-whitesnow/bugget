import { Check, X } from "lucide-react";
import { ReactNode, useRef } from "react";

import {
  SelectFieldLayout,
  SelectMenu,
  SelectMenuItem,
} from "../SelectPrimitives";

export type DropdownOption<T = string> = {
  label: string;
  value: T;
  indicator?: ReactNode;
};

type Props<T = string> = {
  label?: string;
  options: DropdownOption<T>[];
  value: T | T[] | null;
  onChange: (value: T | T[] | null) => void;
  multiple?: boolean;
  onResetValue?: T | T[] | null;
  placeholder?: string;
  className?: string;
  renderValue?: (
    selectedOptions: DropdownOption<T>[],
    allOptions: DropdownOption<T>[]
  ) => ReactNode;
  renderOption?: (option: DropdownOption<T>, isSelected: boolean) => ReactNode;
  renderToken?: (
    option: DropdownOption<T>,
    onRemove: (event?: React.MouseEvent<HTMLElement>) => void
  ) => ReactNode;
};

const MultipleDropdown = <T,>(props: Props<T>) => {
  const {
    label,
    options,
    value,
    onChange,
    multiple = false,
    onResetValue,
    placeholder = "Любой",
    className = "",
    renderValue,
    renderOption,
    renderToken,
  } = props;
  const dropdownRef = useRef<HTMLDivElement>(null);
  const hasCustomResetValue = "onResetValue" in props;

  const isSelected = (val: T) =>
    multiple ? Array.isArray(value) && value.includes(val) : val === value;

  const singleSelectedLabel =
    value !== null && value !== undefined
      ? (options.find((opt) => opt.value === value)?.label ?? "")
      : "";

  const hasValue = multiple
    ? Array.isArray(value) && value.length > 0
    : value !== null && value !== undefined;
  const selectedOptions = multiple
    ? options.filter((option) =>
        Array.isArray(value) ? value.includes(option.value) : false
      )
    : options.filter((option) => option.value === value);
  const selectedCount = selectedOptions.length;

  const handleDropdownClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    onChange(onResetValue ?? (multiple ? [] : null));
    dropdownRef.current?.blur();
  };

  const toggleOption = (option: DropdownOption<T>) => {
    if (multiple) {
      const current = Array.isArray(value) ? value : [];
      const exists = current.includes(option.value);
      const updated = exists
        ? current.filter((v) => v !== option.value)
        : [...current, option.value];
      onChange(updated);
    } else {
      onChange(option.value);
      dropdownRef.current?.blur();
    }
  };

  const handleTokenRemove =
    (option: DropdownOption<T>) => (event?: React.MouseEvent<HTMLElement>) => {
      event?.preventDefault();
      event?.stopPropagation();

      // Prevent daisyUI dropdown opening via focus/focus-within
      const activeElement = document.activeElement as HTMLElement | null;
      activeElement?.blur();
      dropdownRef.current?.blur();

      if (event && event.type !== "click") {
        return;
      }
      toggleOption(option);
    };

  return (
    <div className={`w-full ${className}`}>
      <div className="dropdown w-full" tabIndex={0} ref={dropdownRef}>
        {label && <div className="field-label mb-1">{label}</div>}
        <label
          tabIndex={0}
          className={`block w-full cursor-pointer rounded-box bg-transparent transition-colors "w-full p-1.5 hover:bg-base-200/70 focus-within:bg-base-200/70"`}
        >
          <SelectFieldLayout
            className="text-sm"
            placeholder={placeholder}
            rightSlot={
              hasValue && hasCustomResetValue ? (
                <button
                  type="button"
                  className="mr-1 inline-flex h-5 w-5 cursor-pointer items-center justify-center rounded-full hover:bg-base-300/60"
                  onClick={handleDropdownClick}
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              ) : null
            }
          >
            {renderValue ? (
              renderValue(selectedOptions, options)
            ) : !hasValue ? undefined : (
              <span className="truncate">
                {multiple ? `${selectedCount}` : singleSelectedLabel}
              </span>
            )}
          </SelectFieldLayout>
        </label>

        <SelectMenu
          className="dropdown-content menu"
          beforeItems={
            !multiple && hasCustomResetValue ? (
              <SelectMenuItem
                key="none"
                className={
                  value === null || value === undefined
                    ? "active italic"
                    : "italic"
                }
                contentClassName="text-base-content/50"
                onClick={() => {
                  onChange(null);
                  dropdownRef.current?.blur();
                }}
              >
                Не выбрано
              </SelectMenuItem>
            ) : null
          }
          items={options}
          renderItem={(option) =>
            renderOption ? (
              <li key={String(option.value)}>
                <button
                  onClick={() => {
                    toggleOption(option);
                    dropdownRef.current?.blur();
                  }}
                  className={`w-full cursor-pointer rounded-box px-3 py-1 transition-colors hover:bg-base-200 ${
                    isSelected(option.value) ? "bg-base-200" : ""
                  }`}
                >
                  {renderOption(option, isSelected(option.value))}
                </button>
              </li>
            ) : (
              <SelectMenuItem
                key={String(option.value)}
                isSelected={isSelected(option.value)}
                onClick={() => toggleOption(option)}
                rightSlot={
                  <Check
                    className={`h-4 w-4 ${
                      isSelected(option.value)
                        ? "text-success"
                        : "text-transparent"
                    }`}
                  />
                }
              >
                <span>{option.indicator ?? option.label}</span>
              </SelectMenuItem>
            )
          }
        />
      </div>

      {multiple && selectedOptions.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1.5">
          {selectedOptions.map((option) =>
            renderToken ? (
              <div key={String(option.value)}>
                {renderToken(option, handleTokenRemove(option))}
              </div>
            ) : (
              <span
                key={String(option.value)}
                className="badge h-7 gap-1.5 border border-base-300 px-2.5"
              >
                <span>{option.label}</span>
                <button
                  type="button"
                  onMouseDown={handleTokenRemove(option)}
                  onClick={handleTokenRemove(option)}
                  aria-label={`Убрать ${option.label}`}
                  className="inline-flex h-5 w-5 cursor-pointer items-center justify-center rounded-full hover:bg-base-200"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </span>
            )
          )}
        </div>
      )}
    </div>
  );
};

export default MultipleDropdown;
