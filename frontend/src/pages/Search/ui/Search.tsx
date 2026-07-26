import { SortDropdown } from "@/shared/ui";
import type { SortOption } from "@/shared/ui";
import SearchFilters from "./components/SearchFilters/SearchFilters";
import SearchResults from "./components/SearchResults/SearchResults";
import SearchInput from "./components/SearchInput/SearchInput";
import { useUnit } from "effector-react";
import {
  $sortField,
  $sortDirection,
  updateSortField,
  updateSortDirection,
  searchPageClosed,
  searchPageOpened,
} from "../model";
import { useEffect } from "react";

import "./Search.css";

const Search = () => {
  const [
    sortField,
    sortDirection,
    setSortField,
    setSortDirection,
    onSearchPageClosed,
    onSearchPageOpened,
  ] = useUnit([
    $sortField,
    $sortDirection,
    updateSortField,
    updateSortDirection,
    searchPageClosed,
    searchPageOpened,
  ]);

  const options: SortOption[] = [
    { label: "Дата создания", value: "created" },
    { label: "Дата изменения", value: "updated" },
    { label: "Лучшее совпадение", value: "rank" },
  ];

  useEffect(() => {
    onSearchPageOpened();
    return () => {
      onSearchPageClosed();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onToggleDirectionHandler = () => {
    setSortDirection(sortDirection === "asc" ? "desc" : "asc");
  };

  return (
    <div className="search-layout responsive-inline [--responsive-gap:1.5rem] [--responsive-item-min:17rem]">
      <SearchFilters />

      <div className="responsive-fill">
        <SearchInput />
        <div className="my-3 flex justify-end">
          <SortDropdown
            className="w-fit max-w-full"
            options={options}
            value={sortField}
            direction={sortDirection}
            onChange={setSortField}
            onToggleDirection={onToggleDirectionHandler}
          />
        </div>
        <SearchResults />
      </div>
    </div>
  );
};

export default Search;
