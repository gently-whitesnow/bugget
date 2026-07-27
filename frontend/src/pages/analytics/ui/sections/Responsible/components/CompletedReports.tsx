import { useSearchParams } from "react-router";

import type { AnalyticsResponsibleCompletedReport } from "@/shared/api";

type Props = {
  reports: AnalyticsResponsibleCompletedReport[];
};

const outcomeLabel = (
  outcome: AnalyticsResponsibleCompletedReport["outcome"]
): string => {
  switch (outcome) {
    case "Resolved":
      return "Решён";
    case "Rejected":
      return "Отклонён";
    default:
      return outcome;
  }
};

const outcomeClass = (
  outcome: AnalyticsResponsibleCompletedReport["outcome"]
): string => {
  switch (outcome) {
    case "Resolved":
      return "text-success";
    case "Rejected":
      return "text-error";
    default:
      return "text-base-content/70";
  }
};

const formatDate = (iso: string): string => {
  try {
    return new Date(iso).toLocaleDateString("ru-RU", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    });
  } catch {
    return iso;
  }
};

const CompletedReports = ({ reports }: Props) => {
  const [searchParams, setSearchParams] = useSearchParams();

  // `report_id` необязателен по контракту analytics (см. TopRegressionReports):
  // без id не навигируем, вместо того чтобы открывать репорт №0.
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
        <div className="text-sm font-medium">Завершённые репорты</div>
        <div className="text-xs text-base-content/50">за период</div>
      </div>

      {reports.length === 0 ? (
        <div className="py-6 text-center text-sm text-base-content/50">
          Нет завершённых репортов в этом периоде
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
                <span
                  className={`text-xs font-medium whitespace-nowrap ${outcomeClass(r.outcome)}`}
                >
                  {outcomeLabel(r.outcome)}
                </span>
                <span className="text-xs text-base-content/50 tabular-nums whitespace-nowrap">
                  {formatDate(r.closed_at)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default CompletedReports;
