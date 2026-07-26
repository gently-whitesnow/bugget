import { useCallback, useEffect, useId, useRef, useState } from "react";
import { X } from "lucide-react";

import type { AutocompleteEntity } from "./types";
import useKeyboardNav from "./useKeyboardNav";
import SuggestionList from "./components/SuggestionList";
import Avatar from "../Avatar/Avatar";

import "./Autosuggest.css";

type Props = {
  externalString?: string;
  externalImageUrl?: string | null;
  onSelect: (entity: AutocompleteEntity | null) => void;
  autocompleteFn: (searchString: string) => Promise<AutocompleteEntity[]>;
  width?: number;
  placeholder?: string;
};

const Autosuggest = ({
  externalString,
  externalImageUrl,
  onSelect,
  autocompleteFn,
  width,
  placeholder = "Начните вводить",
}: Props) => {
  const listId = useId();
  const searchInputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const [isOpen, setIsOpen] = useState(false);
  const [searchString, setSearchString] = useState("");
  const [items, setItems] = useState<AutocompleteEntity[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const resetDropdownState = useCallback(() => {
    clearTimeout(timerRef.current);
    setSearchString("");
    setItems([]);
    setIsLoading(false);
    setHasSearched(false);
  }, []);

  const handleSelect = useCallback(
    (item: AutocompleteEntity) => {
      setIsOpen(false);
      resetDropdownState();
      onSelect(item);
    },
    [onSelect, resetDropdownState]
  );

  const handleClose = useCallback(() => {
    setIsOpen(false);
    resetDropdownState();
  }, [resetDropdownState]);

  const { activeIndex, handleKeyDown: handleNavKeyDown } = useKeyboardNav({
    items,
    isOpen,
    onSelect: handleSelect,
    onClose: handleClose,
  });

  const search = useCallback(
    async (value: string) => {
      try {
        const data = await autocompleteFn(value);
        setItems(data);
      } catch (err) {
        console.error(err);
        setItems([]);
      } finally {
        setIsLoading(false);
        setHasSearched(true);
      }
    },
    [autocompleteFn]
  );

  useEffect(() => {
    return () => clearTimeout(timerRef.current);
  }, []);

  useEffect(() => {
    if (isOpen) {
      requestAnimationFrame(() => {
        searchInputRef.current?.focus();
      });
      setIsLoading(true);
      setHasSearched(false);
      search("");
    }
  }, [isOpen, search]);

  useEffect(() => {
    if (!isOpen) return;
    const handleOutsideClick = (event: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        handleClose();
      }
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, [isOpen, handleClose]);

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value;
    setSearchString(value);
    clearTimeout(timerRef.current);
    if (value) {
      setIsLoading(true);
      setHasSearched(false);
      timerRef.current = setTimeout(() => search(value), 300);
    } else {
      setItems([]);
      setIsLoading(false);
      setHasSearched(false);
    }
  };

  const clearSearch = () => {
    clearTimeout(timerRef.current);
    setSearchString("");
    setItems([]);
    setIsLoading(false);
    setHasSearched(false);
    searchInputRef.current?.focus();
  };

  const clearSelection = useCallback(() => {
    resetDropdownState();
    onSelect(null);
    requestAnimationFrame(() => {
      searchInputRef.current?.focus();
    });
  }, [onSelect, resetDropdownState]);

  const handleTriggerKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      setIsOpen(true);
    }
  };

  const widthClass = width ? `w-${width}` : "w-full";
  const showDropdown = isLoading || hasSearched;

  return (
    <div ref={containerRef} className="relative">
      {isOpen ? (
        <div
          className={`group relative rounded-box bg-transparent transition-colors hover:bg-base-200/70 focus-within:bg-base-200/70 ${widthClass}`}
        >
          <input
            ref={searchInputRef}
            role="combobox"
            aria-expanded={true}
            aria-controls={listId}
            aria-activedescendant={
              activeIndex >= 0 ? `${listId}-option-${activeIndex}` : undefined
            }
            aria-autocomplete="list"
            className="w-full rounded-box border-none bg-transparent px-1.5 py-1.5 pr-8 text-sm text-base-content outline-none placeholder:text-base-content/50"
            value={searchString}
            placeholder="Начните вводить..."
            onChange={handleChange}
            onKeyDown={handleNavKeyDown}
            autoComplete="off"
          />
          <button
            className="autosuggest-clear"
            onMouseDown={(e) => e.preventDefault()}
            onClick={
              searchString
                ? clearSearch
                : externalString
                  ? clearSelection
                  : handleClose
            }
            aria-label={
              searchString
                ? "Очистить поиск"
                : externalString
                  ? "Очистить значение"
                  : "Закрыть"
            }
            tabIndex={-1}
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      ) : (
        <div
          className={`autosuggest-trigger rounded-box bg-transparent px-1.5 py-1.5 text-sm transition-colors hover:bg-base-200/70 ${widthClass}`}
          onClick={() => setIsOpen(true)}
          onKeyDown={handleTriggerKeyDown}
          role="button"
          tabIndex={0}
        >
          {externalString && (
            <div className="shrink-0">
              <Avatar src={externalImageUrl ?? undefined} width={6} />
            </div>
          )}
          <span
            className={`flex-1 truncate ${!externalString ? "opacity-50" : ""}`}
          >
            {externalString || placeholder}
          </span>
          {externalString && (
            <button
              type="button"
              className="autosuggest-clear"
              onMouseDown={(e) => e.preventDefault()}
              onClick={(event) => {
                event.stopPropagation();
                clearSelection();
              }}
              aria-label="Очистить значение"
              tabIndex={-1}
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      )}
      {isOpen && showDropdown && (
        <SuggestionList
          items={items}
          activeIndex={activeIndex}
          isLoading={isLoading}
          hasSearched={hasSearched}
          onSelect={handleSelect}
          listId={listId}
        />
      )}
    </div>
  );
};

export default Autosuggest;
