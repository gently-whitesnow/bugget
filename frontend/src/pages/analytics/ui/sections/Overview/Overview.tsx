import { useEffect } from "react";
import { useUnit } from "effector-react";

import type { AnalyticsPeriod } from "@/shared/lib/time";
import {
  PhaseDurationCards,
  ReworkRateCard,
  PhaseTimeDistribution,
  TopRegressionReports,
  PhaseTrendsChart,
} from "../../components/charts";

import {
  $summaryStore,
  $summaryError,
  fetchSummaryFx,
  overviewMounted,
  overviewUnmounted,
  periodChanged,
} from "../../../model/overview";

type Props = {
  period: AnalyticsPeriod;
};

const AnalyticsOverview = ({ period }: Props) => {
  const [summary, isPending, error, onMounted, onUnmounted, onPeriodChanged] =
    useUnit([
      $summaryStore,
      fetchSummaryFx.pending,
      $summaryError,
      overviewMounted,
      overviewUnmounted,
      periodChanged,
    ]);

  // Sync URL period → effector store.
  useEffect(() => {
    onPeriodChanged(period);
  }, [period, onPeriodChanged]);

  useEffect(() => {
    onMounted();
    return () => {
      onUnmounted();
    };
  }, [onMounted, onUnmounted]);

  if (isPending && !summary) {
    return (
      <div className="py-12 flex items-center justify-center">
        <span className="loading loading-spinner loading-md"></span>
      </div>
    );
  }

  if (error && !summary) {
    return (
      <div className="rounded-md border border-error/30 bg-error/5 p-4 text-sm text-error">
        Не удалось загрузить аналитику: {error}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <PhaseDurationCards data={summary?.avg_phase_duration_days ?? null} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
        <ReworkRateCard
          reworkRate={summary?.rework_rate ?? null}
          avgRegressionCyclesWhenPresent={
            summary?.avg_regression_cycles_when_present ?? null
          }
        />
        <PhaseTimeDistribution
          data={summary?.phase_time_distribution ?? null}
        />
      </div>

      <TopRegressionReports reports={summary?.top_regression_reports ?? []} />

      <PhaseTrendsChart trends={summary?.phase_trends_weekly ?? []} />
    </div>
  );
};

export default AnalyticsOverview;
