import { useSearchParams } from "react-router";
import type { TopRegressionReport, WireInt64 } from "@/shared/api";

type Props = {
  reports: TopRegressionReport[];
};

const TopRegressionReports = ({ reports }: Props) => {
  const [searchParams, setSearchParams] = useSearchParams();

  // `report_id` обязателен по контракту analytics и приходит строкой канона
  // `Int64String`: в адрес он уходит дословно, `Number(...)` к нему не применяется —
  // идентификатор за 2^53−1 округлился бы и открыл соседний репорт.
  const openReport = (reportId: WireInt64) => {
    const next = new URLSearchParams(searchParams);
    next.set("section", "report");
    next.set("report", reportId);
    setSearchParams(next);
  };

  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-sm font-medium">Топ репортов с регрессиями</div>
        <div className="text-xs text-base-content/50">до 10</div>
      </div>

      {reports.length === 0 ? (
        <div className="py-6 text-center text-sm text-base-content/50">
          Нет регрессионных репортов в этом периоде
        </div>
      ) : (
        <ul className="flex flex-col divide-y divide-base-200">
          {reports.map((r) => (
            <li key={r.reportId}>
              <button
                type="button"
                onClick={() => openReport(r.reportId)}
                className="w-full text-left py-2 px-1 flex items-center gap-3 hover:bg-base-200 rounded-sm transition-colors"
              >
                <span className="text-xs font-mono text-base-content/50 tabular-nums">
                  #{r.reportId}
                </span>
                <span className="flex-1 truncate text-sm">{r.title}</span>
                <span className="text-xs font-medium tabular-nums whitespace-nowrap">
                  {r.regressionCycles}{" "}
                  <span className="text-base-content/50">циклов</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default TopRegressionReports;
