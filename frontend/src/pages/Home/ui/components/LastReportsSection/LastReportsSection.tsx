import { useEffect, useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { Link } from "react-router-dom";
import { useUnit } from "effector-react";

import { $reportsUsersStore } from "@/entities/report-list";
import { ReportCard } from "@/entities/report";
import { lastReportsDashboardTake } from "@/shared/config";
import { buildFullAppUrl } from "@/shared/lib/buildFullUrl";
import type { ListReportsResponse } from "@/entities/report-list";

type Props = {
  data: ListReportsResponse;
  className?: string;
  onExpand: (isOpen: boolean) => void;
  defaultExpanded?: boolean;
};

const LastReportsSection = ({
  data,
  className,
  onExpand,
  defaultExpanded = false,
}: Props) => {
  const usersStore = useUnit($reportsUsersStore);
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);

  const searchPath = "search";
  const fullUrl = buildFullAppUrl(searchPath);

  const handleToggle = () => {
    if (!isExpanded && onExpand) {
      onExpand(!isExpanded);
    }
    setIsExpanded(!isExpanded);
  };

  useEffect(() => {
    if (defaultExpanded) {
      setIsExpanded(true);
      onExpand(defaultExpanded);
    }
  }, [defaultExpanded, onExpand]);

  return (
    <section className="flex flex-col gap-2">
      <div
        className="text-sm text-base-content/70 flex items-center gap-1 cursor-pointer select-none"
        onClick={handleToggle}
      >
        Недавно решённые
        {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
      </div>
      {isExpanded && !!data.reports.length && (
        <>
          <div className="flex flex-col gap-1">
            {data.reports.map((report) => (
              <ReportCard
                key={report.id}
                report={report}
                usersStore={usersStore}
                className={className}
              />
            ))}
          </div>
          {data.total > lastReportsDashboardTake && (
            <div>
              <span className="text-xs text-base-content/50">
                Больше репортов{" "}
                <Link to={fullUrl} className="underline">
                  в поиске
                </Link>{" "}
              </span>
            </div>
          )}
        </>
      )}
    </section>
  );
};

export default LastReportsSection;
