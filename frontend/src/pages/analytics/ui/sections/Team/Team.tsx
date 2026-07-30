import { useEffect, useMemo } from "react";
import { useUnit } from "effector-react";

import { $workspaces } from "@/shared/model";
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
  fetchTeamSummaryFx,
  teamMounted,
  teamUnmounted,
  periodChanged,
  teamIdChanged,
} from "../../../model/team";
import TeamSelector from "./components/TeamSelector";

type Props = {
  period: AnalyticsPeriod;
  teamId: string | null;
  onTeamChange: (teamId: string) => void;
};

const AnalyticsTeam = ({ period, teamId, onTeamChange }: Props) => {
  const [
    workspaces,
    summary,
    isPending,
    error,
    onMounted,
    onUnmounted,
    onPeriodChanged,
    onTeamIdChanged,
  ] = useUnit([
    $workspaces,
    $summaryStore,
    fetchTeamSummaryFx.pending,
    $summaryError,
    teamMounted,
    teamUnmounted,
    periodChanged,
    teamIdChanged,
  ]);

  // Workspace в self-hosted один.
  const currentWorkspace = useMemo(() => workspaces[0], [workspaces]);

  const teams = useMemo(
    () => currentWorkspace?.teams ?? [],
    [currentWorkspace]
  );

  // Если teamId из URL не задан, но команда одна — авто-выбираем.
  const effectiveTeamId = useMemo(() => {
    if (teamId) return teamId;
    if (teams.length === 1) return String(teams[0].id);
    return null;
  }, [teamId, teams]);

  // Авто-проставляем единственную команду в URL, чтобы остальные сэмплы
  // (model) знали о выборе.
  useEffect(() => {
    if (!teamId && teams.length === 1) {
      onTeamChange(String(teams[0].id));
    }
  }, [teamId, teams, onTeamChange]);

  // Sync props → model.
  useEffect(() => {
    onPeriodChanged(period);
  }, [period, onPeriodChanged]);

  useEffect(() => {
    onTeamIdChanged(effectiveTeamId);
  }, [effectiveTeamId, onTeamIdChanged]);

  useEffect(() => {
    onMounted();
    return () => {
      onUnmounted();
    };
  }, [onMounted, onUnmounted]);

  const noTeamSelected = !effectiveTeamId;

  return (
    <div className="flex flex-col gap-4">
      <TeamSelector
        teams={teams}
        value={effectiveTeamId}
        onChange={onTeamChange}
      />

      {noTeamSelected ? (
        <div className="rounded-md border border-base-300 bg-base-100 p-6 text-sm text-base-content/60">
          Выберите команду, чтобы посмотреть аналитику.
        </div>
      ) : isPending && !summary ? (
        <div className="py-12 flex items-center justify-center">
          <span className="loading loading-spinner loading-md"></span>
        </div>
      ) : error && !summary ? (
        <div className="rounded-md border border-error/30 bg-error/5 p-4 text-sm text-error">
          Не удалось загрузить аналитику команды: {error}
        </div>
      ) : (
        <>
          <PhaseDurationCards data={summary?.avgPhaseDurationDays ?? null} />

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
            <ReworkRateCard
              reworkRate={summary?.reworkRate ?? null}
              avgRegressionCyclesWhenPresent={
                summary?.avgRegressionCyclesWhenPresent ?? null
              }
            />
            <PhaseTimeDistribution
              data={summary?.phaseTimeDistribution ?? null}
            />
          </div>

          <TopRegressionReports reports={summary?.topRegressionReports ?? []} />

          <PhaseTrendsChart trends={summary?.phaseTrendsWeekly ?? []} />
        </>
      )}
    </div>
  );
};

export default AnalyticsTeam;
