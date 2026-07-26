import { useSearchParams } from "react-router";

import type { AnalyticsResponsibleParticipatedReport } from "@/shared/api";

type Props = {
  reports: AnalyticsResponsibleParticipatedReport[];
};

const phaseLabel = (
  phase: AnalyticsResponsibleParticipatedReport["current_phase"]
): string => {
  switch (phase) {
    case "Test":
      return "Test";
    case "Fix":
      return "Fix";
    default:
      return phase;
  }
};

const ParticipatedReports = ({ reports }: Props) => {
  const [searchParams, setSearchParams] = useSearchParams();

  const openReport = (reportId: number) => {
    const next = new URLSearchParams(searchParams);
    next.set("section", "report");
    next.set("report", String(reportId));
    setSearchParams(next);
  };

  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-sm font-medium">Активные репорты</div>
        <div className="text-xs text-base-content/50">
          участие в текущей фазе
        </div>
      </div>

      {reports.length === 0 ? (
        <div className="py-6 text-center text-sm text-base-content/50">
          Нет активных репортов
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
                  {phaseLabel(r.current_phase)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default ParticipatedReports;
