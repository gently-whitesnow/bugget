import { useEffect } from "react";
import { useUnit } from "effector-react";

import {
  $reportStore,
  $reportError,
  fetchReportFx,
  reportMounted,
  reportUnmounted,
  reportIdChanged,
} from "../../../model/report";
import type { WireInt64 } from "@/shared/api";
import PhaseTimeline from "./components/PhaseTimeline";
import BugsByStatusGrid from "./components/BugsByStatusGrid";

type Props = {
  reportId: WireInt64 | undefined;
};

const AnalyticsReport = ({ reportId }: Props) => {
  const [report, isPending, error, onMounted, onUnmounted, onReportIdChanged] =
    useUnit([
      $reportStore,
      fetchReportFx.pending,
      $reportError,
      reportMounted,
      reportUnmounted,
      reportIdChanged,
    ]);

  // Sync props → model.
  useEffect(() => {
    onReportIdChanged(reportId ?? null);
  }, [reportId, onReportIdChanged]);

  useEffect(() => {
    onMounted();
    return () => {
      onUnmounted();
    };
  }, [onMounted, onUnmounted]);

  if (reportId === undefined) {
    return (
      <div className="rounded-md border border-base-300 bg-base-100 p-6 text-sm text-base-content/60">
        Выберите репорт из списка «Топ репортов с регрессиями» в разделе
        «Сводно», чтобы посмотреть его таймлайн фаз.
      </div>
    );
  }

  if (isPending && !report) {
    return (
      <div className="py-12 flex items-center justify-center">
        <span className="loading loading-spinner loading-md"></span>
      </div>
    );
  }

  if (error && !report) {
    return (
      <div className="rounded-md border border-error/30 bg-error/5 p-4 text-sm text-error">
        Не удалось загрузить аналитику репорта: {error}
      </div>
    );
  }

  if (!report) {
    return null;
  }

  // Счётчик необязателен по контракту analytics (см. TopRegressionReports):
  // отсутствующее значение читаем как ноль — регрессии не было.
  const regressionDetected = (report.bugsAddedDuringRegression ?? 0) > 0;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-base font-semibold">
          Репорт{" "}
          <span className="font-mono text-base-content/70">
            #{report.reportId}
          </span>
        </h2>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="rounded-md border border-base-300 bg-base-100 p-4">
          <div className="text-xs uppercase tracking-wide text-base-content/60">
            Regression-циклы
          </div>
          <div className="mt-2 text-2xl font-semibold tabular-nums">
            {report.regressionCycles}
          </div>
          <div className="mt-1 text-[11px] text-base-content/50">
            Полных циклов Test → Fix → Test
          </div>
        </div>

        <div
          className={`rounded-md border p-4 ${
            regressionDetected
              ? "border-error/40 bg-error/5"
              : "border-base-300 bg-base-100"
          }`}
        >
          <div
            className={`text-xs uppercase tracking-wide ${
              regressionDetected ? "text-error" : "text-base-content/60"
            }`}
          >
            Багов добавлено в регрессии
          </div>
          <div
            className={`mt-2 text-2xl font-semibold tabular-nums ${
              regressionDetected ? "text-error" : ""
            }`}
          >
            {report.bugsAddedDuringRegression}
          </div>
          <div className="mt-1 text-[11px] text-base-content/50">
            {regressionDetected
              ? "Сигнал регрессии — добавлены при повторных Test-фазах"
              : "В повторных Test-фазах багов не добавлено"}
          </div>
        </div>
      </div>

      <PhaseTimeline entries={report.phaseTimeline} />

      <BugsByStatusGrid data={report.bugsByStatus} />
    </div>
  );
};

export default AnalyticsReport;
