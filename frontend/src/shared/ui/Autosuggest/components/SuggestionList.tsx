import { useEffect, useRef } from "react";

import Avatar from "../../Avatar/Avatar";
import type { AutocompleteEntity } from "../types";

type Props = {
  items: AutocompleteEntity[];
  activeIndex: number;
  isLoading: boolean;
  hasSearched: boolean;
  onSelect: (item: AutocompleteEntity) => void;
  listId: string;
};

const SuggestionList = ({
  items,
  activeIndex,
  isLoading,
  hasSearched,
  onSelect,
  listId,
}: Props) => {
  const listRef = useRef<HTMLUListElement>(null);

  useEffect(() => {
    if (activeIndex < 0 || !listRef.current) return;
    const activeEl = listRef.current.children[activeIndex] as HTMLElement;
    activeEl?.scrollIntoView({ block: "nearest" });
  }, [activeIndex]);

  if (isLoading) {
    return (
      <div
        className="absolute top-full left-0 right-0 z-50 mt-1 rounded-[14px] border border-base-content/15 bg-base-100 p-3 shadow-lg"
        role="status"
      >
        <div className="flex items-center justify-center">
          <span className="loading loading-spinner loading-sm" />
        </div>
      </div>
    );
  }

  if (hasSearched && items.length === 0) {
    return (
      <div className="absolute top-full left-0 right-0 z-50 mt-1 rounded-[14px] border border-base-content/15 bg-base-100 px-3 py-2 shadow-lg">
        <span className="text-sm text-base-content/50">Ничего не найдено</span>
      </div>
    );
  }

  if (items.length === 0) return null;

  return (
    <ul
      ref={listRef}
      id={listId}
      role="listbox"
      className="absolute top-full left-0 right-0 z-50 mt-1 max-h-64 overflow-y-auto rounded-[14px] border border-base-content/15 bg-base-100 p-1 shadow-lg"
    >
      {items.map((item, index) => (
        <li
          key={item.id}
          id={`${listId}-option-${index}`}
          role="option"
          aria-selected={index === activeIndex}
          className={`flex cursor-pointer select-none items-center gap-2 rounded-box px-2 py-1.5 ${
            index === activeIndex ? "bg-base-200" : "hover:bg-base-200"
          }`}
          onMouseDown={(e) => {
            e.preventDefault();
            onSelect(item);
          }}
        >
          <div className="shrink-0">
            <Avatar src={item.imageUrl ?? undefined} width={6} />
          </div>
          <span className="truncate text-sm">{item.display}</span>
        </li>
      ))}
    </ul>
  );
};

export default SuggestionList;
