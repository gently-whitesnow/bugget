import { useState, useEffect, useCallback, useMemo } from "react";
import { useUnit } from "effector-react";
import { X, Search, Check } from "lucide-react";
import {
  $searchResults,
  $selectedBoards,
  $isSearching,
  $isLoadingSelected,
  searchBoardsFx,
  loadBoardsByIdsFx,
} from "../../../../../model";
import type { KaitenBoard } from "../../../../../model/types";
import { SettingLabel } from "../SettingLabel";
import { CustomSettingProps } from ".";

const keyOf = (arr: string[]) => arr.join("|");

export const KaitenBoardsSelect = ({
  title,
  description,
  values,
  isUpdating,
  onSave,
}: CustomSettingProps) => {
  const [localValues, setLocalValues] = useState<string[]>(() =>
    values.map((v) => v.trim()).filter(Boolean)
  );
  const [searchQuery, setSearchQuery] = useState("");

  const [searchResults, selectedBoards, isSearching, isLoadingSelected] =
    useUnit([
      $searchResults,
      $selectedBoards,
      $isSearching,
      $isLoadingSelected,
    ]);
  const [runLoadBoardsByIds, runSearchBoards] = useUnit([
    loadBoardsByIdsFx,
    searchBoardsFx,
  ]);

  const filteredValues = useMemo(
    () => values.map((v) => v.trim()).filter(Boolean),
    [values]
  );

  const filteredKey = useMemo(() => keyOf(filteredValues), [filteredValues]);
  const localKey = useMemo(() => keyOf(localValues), [localValues]);

  const isSynced = localKey === filteredKey;

  useEffect(() => {
    setLocalValues(filteredValues);
  }, [filteredKey, filteredValues]);

  useEffect(() => {
    if (filteredValues.length > 0) runLoadBoardsByIds(filteredValues);
  }, [filteredKey, filteredValues, runLoadBoardsByIds]);

  useEffect(() => {
    runSearchBoards(searchQuery || undefined);
  }, [searchQuery, runSearchBoards]);

  const boardTitleById = useMemo(() => {
    const map = new Map<string, string>();
    for (const b of selectedBoards) map.set(String(b.id), b.title);
    return map;
  }, [selectedBoards]);

  const getBoardTitle = useCallback(
    (boardId: string) => boardTitleById.get(boardId) ?? `#${boardId}`,
    [boardTitleById]
  );

  const handleAddBoard = useCallback(
    (board: KaitenBoard) => {
      const boardId = String(board.id);

      setLocalValues((prev) => {
        if (prev.includes(boardId)) return prev;
        const next = [...prev, boardId];
        onSave(next);
        return next;
      });

      setSearchQuery("");
      (document.activeElement as HTMLElement | null)?.blur();
    },
    [onSave]
  );

  const handleRemoveBoard = useCallback(
    (boardId: string) => {
      setLocalValues((prev) => {
        const next = prev.filter((id) => id !== boardId);
        onSave(next);
        return next;
      });
    },
    [onSave]
  );

  return (
    <div className="py-4 border-b border-base-300/50 last:border-b-0">
      <div className="flex items-start justify-between mb-3">
        <SettingLabel title={title} description={description} />
        {isSynced && (
          <div className="flex items-center gap-1 text-xs text-success">
            <Check className="w-3 h-3" />
            Сохранено
          </div>
        )}
      </div>

      <div className="flex flex-wrap gap-2 mb-3">
        {localValues.map((boardId) => (
          <div
            key={boardId}
            className="flex items-center gap-1 px-3 py-1.5 bg-base-200 rounded-lg text-sm group"
          >
            {isLoadingSelected && (
              <span className="loading loading-spinner loading-xs" />
            )}
            <span>{getBoardTitle(boardId)}</span>
            <button
              className="opacity-0 group-hover:opacity-100 transition-opacity ml-1 hover:text-error"
              onClick={() => handleRemoveBoard(boardId)}
              disabled={isUpdating}
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        ))}
      </div>

      <div className="dropdown dropdown-bottom w-full">
        <div className="relative w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-base-content/40 z-10" />
          <input
            tabIndex={0}
            type="text"
            className="input input-bordered input-sm w-full pl-9"
            placeholder="Поиск доски..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            disabled={isUpdating}
          />
        </div>

        <ul
          tabIndex={0}
          className="dropdown-content menu bg-base-100 rounded-lg border border-base-300 shadow-lg z-50 w-full max-h-60 overflow-y-auto mt-1 p-0"
        >
          {isSearching ? (
            <li className="p-4 text-center">
              <span className="loading loading-spinner loading-sm" />
            </li>
          ) : searchResults.length === 0 ? (
            <li className="p-4 text-center text-base-content/50 text-sm">
              Доски не найдены
            </li>
          ) : (
            searchResults.map((board: KaitenBoard) => {
              const id = String(board.id);
              const isSelected = localValues.includes(id);

              return (
                <li key={board.id}>
                  <button
                    className={`flex items-center justify-between rounded-none ${
                      isSelected ? "bg-primary/10 text-primary" : ""
                    }`}
                    onClick={() => handleAddBoard(board)}
                    disabled={isSelected || isUpdating}
                  >
                    <span>{board.title}</span>
                    {isSelected && <Check className="w-4 h-4" />}
                  </button>
                </li>
              );
            })
          )}
        </ul>
      </div>
    </div>
  );
};
