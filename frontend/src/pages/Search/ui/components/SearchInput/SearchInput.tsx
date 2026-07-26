import { Search } from "lucide-react";
import { useState, useMemo } from "react";
import { debounce } from "throttle-debounce";
import { useUnit } from "effector-react";

import { updateQuery } from "../../../model";

const SearchInput = () => {
  const onUpdateQuery = useUnit(updateQuery);
  const [value, setValue] = useState("");

  const debouncedUpdateQuery = useMemo(
    () => debounce(300, (val: string) => onUpdateQuery(val)),
    [onUpdateQuery]
  );

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setValue(val);
    debouncedUpdateQuery(val);
  };

  return (
    <label className="flex min-h-[42px] w-full items-center gap-2 rounded-box border border-base-content/15 bg-base-100 px-3 py-2 text-left transition-colors hover:bg-base-200 focus-within:bg-base-200">
      <Search className="w-4 h-4 text-base-content/50" />
      <input
        type="search"
        className="grow bg-transparent focus:outline-none"
        placeholder="Начните вводить для поиска"
        value={value}
        onChange={handleChange}
      />
    </label>
  );
};

export default SearchInput;
