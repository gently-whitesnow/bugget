import { useSearchParams } from "react-router";
import type { TopRegressionReport } from "@/shared/api";

type Props = {
  reports: TopRegressionReport[];
};

const TopRegressionReports = ({ reports }: Props) => {
  const [searchParams, setSearchParams] = useSearchParams();

  // Контракт analytics помечает обязательным только `title`, поэтому `report_id`
  // приходит как `number | undefined`. Бекенд его всегда отдаёт; пока `required`
  // в specs/contracts/analytics/openapi.yaml не восстановлен, без id просто
  // не навигируем — открывать репорт №0 хуже, чем не открывать никакой.
  const openReport = (reportId: number | undefined) => {
    if (reportId === undefined) return;

    const next = new URLSearchParams(searchParams);
    next.set("section", "report");
    next.set("report", String(reportId));
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
            <li key={r.report_id}>
              <button
                type="button"
                onClick={() => openReport(r.report_id)}
                className="w-full text-left py-2 px-1 flex items-center gap-3 hover:bg-base-200 rounded-sm transition-colors"
              >
                <span className="text-xs font-mono text-base-content/50 tabular-nums">
                  #{r.report_id}
                </span>
                <span className="flex-1 truncate text-sm">{r.title}</span>
                <span className="text-xs font-medium tabular-nums whitespace-nowrap">
                  {r.regression_cycles}{" "}
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
