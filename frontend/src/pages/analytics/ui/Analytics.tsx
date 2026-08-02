import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";

import Overview from "./sections/Overview/Overview";
import Team from "./sections/Team/Team";
import Responsible from "./sections/Responsible/Responsible";
import Report from "./sections/Report/Report";
import { parsePeriod, type AnalyticsPeriod } from "@/shared/lib/time";
import { isWireInt64 } from "@/shared/api";

import {
  parseSection,
  defaultSection,
  type AnalyticsSection,
} from "../lib/section";
import PeriodFilter from "./components/PeriodFilter";
import CrossSectionTabs from "./components/CrossSectionTabs";

const Analytics = () => {
  const [searchParams, setSearchParams] = useSearchParams();

  const period: AnalyticsPeriod = useMemo(
    () => parsePeriod(searchParams.get("period") ?? undefined),
    [searchParams]
  );
  const section: AnalyticsSection = useMemo(
    () => parseSection(searchParams.get("section") ?? undefined),
    [searchParams]
  );
  const teamIdParam = searchParams.get("team");
  const userIdParam = searchParams.get("user");
  const reportIdParam = searchParams.get("report");
  // `?report=` приходит из адреса, а не с провода, поэтому канон проверяется
  // здесь: неканоничный сегмент к ручке не уходит. Разбирать его в число нельзя —
  // идентификатор за 2^53−1 округлился бы и открыл соседний репорт.
  const reportIdFromQuery = useMemo(
    () => (isWireInt64(reportIdParam) ? reportIdParam : undefined),
    [reportIdParam]
  );

  const updateQuery = useCallback(
    (mutate: (next: URLSearchParams) => void) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          mutate(next);
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  const handlePeriodChange = (next: AnalyticsPeriod) => {
    updateQuery((q) => {
      q.set("period", next);
    });
  };

  const handleSectionChange = (next: AnalyticsSection) => {
    updateQuery((q) => {
      if (next === defaultSection) {
        q.delete("section");
      } else {
        q.set("section", next);
      }
      // Drill-down параметры (?user, ?team, ?report) намеренно НЕ сбрасываем
      // при смене таба: возврат на секцию восстанавливает прежний выбор.
    });
  };

  const handleTeamChange = useCallback(
    (nextTeamId: string) => {
      updateQuery((q) => {
        q.set("team", nextTeamId);
      });
    },
    [updateQuery]
  );

  const handleUserChange = useCallback(
    (nextUserId: string | null) => {
      updateQuery((q) => {
        if (nextUserId) {
          q.set("user", nextUserId);
        } else {
          q.delete("user");
        }
      });
    },
    [updateQuery]
  );

  return (
    <div className="flex flex-col gap-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-lg font-semibold">Аналитика</h1>
        <PeriodFilter value={period} onChange={handlePeriodChange} />
      </div>

      <CrossSectionTabs value={section} onChange={handleSectionChange} />

      {section === "overview" && <Overview period={period} />}
      {section === "team" && (
        <Team
          period={period}
          teamId={teamIdParam}
          onTeamChange={handleTeamChange}
        />
      )}
      {section === "responsible" && (
        <Responsible
          period={period}
          userId={userIdParam}
          onUserChange={handleUserChange}
        />
      )}
      {section === "report" && <Report reportId={reportIdFromQuery} />}
    </div>
  );
};

export default Analytics;
