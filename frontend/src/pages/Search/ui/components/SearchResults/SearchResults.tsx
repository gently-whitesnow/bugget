import { useUnit } from "effector-react";
import { ChevronDown } from "lucide-react";

import { $searchResult, $usersStore, loadMore } from "../../../model";
import { ReportCard } from "@/entities/report";

const SearchResults = () => {
  const [searchResult, usersStore] = useUnit([$searchResult, $usersStore]);
  const loadMoreHandler = useUnit(loadMore);

  return (
    <div className="flex flex-col gap-1">
      {searchResult?.reports?.map((report) => (
        <ReportCard key={report.id} report={report} usersStore={usersStore} />
      ))}
      {searchResult?.total > (searchResult?.reports?.length || 0) && (
        <button
          onClick={loadMoreHandler}
          className="btn btn-outline btn-secondary"
        >
          <ChevronDown className="w-12 h-12" />
        </button>
      )}
    </div>
  );
};

export default SearchResults;
