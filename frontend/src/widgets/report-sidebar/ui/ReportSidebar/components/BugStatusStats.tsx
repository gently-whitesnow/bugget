import { useUnit } from "effector-react";
import { X } from "lucide-react";
import { BugStatuses, bugStatusMap } from "@/shared/config";
import {
  $reportBugsStore,
  $bugStatusFilterStore,
  setBugStatusFilterEvent,
} from "@/entities/report";
import type { StatusMeta } from "@/shared/lib/types";

const BugStatusStats = () => {
  const bugs = useUnit($reportBugsStore);
  const activeFilter = useUnit($bugStatusFilterStore);
  const setBugStatusFilter = useUnit(setBugStatusFilterEvent);

  const bugsByStatus = (bugs || []).reduce(
    (acc, bug) => {
      acc[bug.status] = (acc[bug.status] || 0) + 1;
      return acc;
    },
    // Ключ — значение статуса с провода; перечислены в `BugStatuses`.
    {} as Partial<Record<BugStatuses, number>>
  );

  const statuses = [
    BugStatuses.FIXED,
    BugStatuses.OPEN,
    BugStatuses.VERIFIED,
    BugStatuses.REJECTED,
  ];

  const renderStatusItem = (
    status: BugStatuses,
    count: number,
    statusMeta: StatusMeta | undefined
  ) => {
    if (count === 0 || !statusMeta) return null;
    const Icon = statusMeta.icon;
    const isActive = activeFilter === status;
    return (
      <button
        key={status}
        className={`tooltip tooltip-bottom flex items-center gap-1 rounded cursor-pointer transition-all ${
          isActive
            ? "ring-1 ring-current opacity-100"
            : "opacity-70 hover:opacity-100"
        }`}
        data-tip={statusMeta.title}
        onClick={() => setBugStatusFilter(status)}
      >
        <Icon className={`w-4 h-4 ${statusMeta.iconColor}`} />
        <span className="text-sm">{count}</span>
      </button>
    );
  };

  return (
    <div className="flex gap-2 items-center px-1.5">
      {statuses.map((status) =>
        renderStatusItem(
          status,
          bugsByStatus[status] || 0,
          bugStatusMap[status]
        )
      )}
      {activeFilter !== null && (
        <button
          className="tooltip tooltip-bottom flex items-center opacity-70 hover:opacity-100 cursor-pointer transition-opacity"
          data-tip="Сбросить фильтр"
          onClick={() => setBugStatusFilter(null)}
        >
          <X className="w-4 h-4" />
        </button>
      )}
    </div>
  );
};

export default BugStatusStats;
