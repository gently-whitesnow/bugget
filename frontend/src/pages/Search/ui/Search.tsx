import { SortDropdown } from "@/shared/ui";
import type { SortOption } from "@/shared/ui";
import SearchFilters from "./components/SearchFilters/SearchFilters";
import SearchResults from "./components/SearchResults/SearchResults";
import SearchInput from "./components/SearchInput/SearchInput";
import { useUnit } from "effector-react";
import {
  $sortField,
  $sortDirection,
  $betaReportsFilter,
  updateSortField,
  updateSortDirection,
  updateBetaReportsFilter,
  searchPageClosed,
  searchPageOpened,
  type BetaReportsFilter,
} from "../model";
import { useEffect } from "react";
import { useSearchParams } from "react-router-dom";

import "./Search.css";

const validBetaValues: BetaReportsFilter[] = ["include", "exclude", "only"];

const Search = () => {
  const [
    sortField,
    sortDirection,
    setSortField,
    setSortDirection,
    onSearchPageClosed,
    onSearchPageOpened,
    betaReportsFilter,
    setBetaReportsFilter,
  ] = useUnit([
    $sortField,
    $sortDirection,
    updateSortField,
    updateSortDirection,
    searchPageClosed,
    searchPageOpened,
    $betaReportsFilter,
    updateBetaReportsFilter,
  ]);

  const [searchParams, setSearchParams] = useSearchParams();

  const options: SortOption[] = [
    { label: "Дата создания", value: "created" },
    { label: "Дата изменения", value: "updated" },
    { label: "Лучшее совпадение", value: "rank" },
  ];

  // Однократно при монтировании читаем ?betaReports= и инициализируем стор.
  useEffect(() => {
    const raw = searchParams.get("betaReports");
    if (raw && (validBetaValues as string[]).includes(raw)) {
      setBetaReportsFilter(raw as BetaReportsFilter);
    }
    onSearchPageOpened();
    return () => {
      onSearchPageClosed();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Sync стора в URL.
  useEffect(() => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (betaReportsFilter === "include") {
          next.delete("betaReports");
        } else {
          next.set("betaReports", betaReportsFilter);
        }
        return next;
      },
      { replace: true }
    );
  }, [betaReportsFilter, setSearchParams]);

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
